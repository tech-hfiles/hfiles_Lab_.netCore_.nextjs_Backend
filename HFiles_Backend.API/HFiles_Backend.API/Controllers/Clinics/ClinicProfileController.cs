﻿using HFiles_Backend.Application.Common;
using HFiles_Backend.Application.DTOs.Clinics.Profile;
using HFiles_Backend.Application.DTOs.Users;
using HFiles_Backend.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HFiles_Backend.API.Controllers.Clinics
{
    [Route("api/")]
    [ApiController]
    public class ClinicProfileController(
        ILogger<ClinicProfileController> logger,
        IClinicRepository clinicRepository,
        IClinicSuperAdminRepository clinicSuperAdminRepository,
        IClinicMemberRepository clinicMemberRepository,
        IUserRepository userRepository,
        IWebHostEnvironment env,
        S3StorageService s3Service
        ) : ControllerBase
    {
        private readonly ILogger<ClinicProfileController> _logger = logger;
        private readonly IClinicRepository _clinicRepository = clinicRepository;
        private readonly IClinicSuperAdminRepository _clinicSuperAdminRepository = clinicSuperAdminRepository;
        private readonly IClinicMemberRepository _clinicMemberRepository = clinicMemberRepository;
        private readonly IUserRepository _userRepository = userRepository;
        private readonly IWebHostEnvironment _env = env;
        private readonly S3StorageService _s3Service = s3Service;





        // Clinic Profile Update
        [HttpPatch("clinics/update")]
        [Authorize(Policy = "SuperAdminOrAdminPolicy")]
        public async Task<IActionResult> UpdateClinicProfile([FromForm] ClinicProfileUpdate dto, IFormFile? ProfilePhoto)
        {
            HttpContext.Items["Log-Category"] = "Clinic Management";
            _logger.LogInformation("UpdateClinicProfile started for ClinicId: {ClinicId}", dto.ClinicId);

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                _logger.LogWarning("Model validation failed for ClinicId: {ClinicId}. Errors: {Errors}", dto.ClinicId, string.Join(", ", errors));
                return BadRequest(ApiResponseFactory.Fail(errors));
            }

            var transaction = await _clinicRepository.BeginTransactionAsync();

            try
            {
                var clinic = await _clinicRepository.GetByIdAsync(dto.ClinicId);
                if (clinic == null)
                {
                    _logger.LogWarning("Clinic not found for ID: {ClinicId}", dto.ClinicId);
                    return NotFound(ApiResponseFactory.Fail($"Clinic with ID {dto.ClinicId} not found."));
                }

                if (!string.IsNullOrWhiteSpace(dto.Address))
                {
                    clinic.Address = dto.Address;
                    _logger.LogInformation("Updated address for ClinicId: {ClinicId}", dto.ClinicId);
                }

                if (ProfilePhoto is { Length: > 0 })
                {
                    var tempFolder = Path.Combine(_env.WebRootPath ?? "wwwroot", "temp-profiles");
                    if (!Directory.Exists(tempFolder)) Directory.CreateDirectory(tempFolder);

                    DateTime createdAt = DateTimeOffset.FromUnixTimeSeconds(clinic.CreatedAtEpoch).UtcDateTime;
                    string formattedTime = createdAt.ToString("dd-MM-yyyy_HH-mm-ss");
                    string fileName = $"{Path.GetFileNameWithoutExtension(ProfilePhoto.FileName)}_{formattedTime}{Path.GetExtension(ProfilePhoto.FileName)}";
                    var tempFilePath = Path.Combine(tempFolder, fileName);

                    try
                    {
                        await using (var stream = new FileStream(tempFilePath, FileMode.Create))
                        {
                            await ProfilePhoto.CopyToAsync(stream);
                        }

                        string s3Key = $"profiles/{fileName}";
                        var s3Url = await _s3Service.UploadFileToS3(tempFilePath, s3Key);
                        clinic.ProfilePhoto = s3Url;

                        _logger.LogInformation("Profile photo updated for ClinicId: {ClinicId}. File saved: {FileName}", dto.ClinicId, fileName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error uploading profile photo for ClinicId: {ClinicId}", dto.ClinicId);
                        return StatusCode(500, ApiResponseFactory.Fail("Failed to upload profile photo."));
                    }
                    finally
                    {
                        try
                        {
                            if (System.IO.File.Exists(tempFilePath))
                                System.IO.File.Delete(tempFilePath);
                        }
                        catch (Exception cleanupEx)
                        {
                            _logger.LogWarning(cleanupEx, "Failed to clean up temp file: {Path}", tempFilePath);
                        }
                    }
                }

                _clinicRepository.Update(clinic);
                await _clinicRepository.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Profile update completed successfully for ClinicId: {ClinicId}", dto.ClinicId);

                return Ok(ApiResponseFactory.Success(new
                {
                    ClinicId = clinic.Id,
                    UpdatedAddress = clinic.Address,
                    UpdatedProfilePhoto = clinic.ProfilePhoto
                }, "Profile updated successfully."));
            }
            finally
            {
                if (transaction.GetDbTransaction().Connection != null)
                {
                    await transaction.RollbackAsync();
                }
            }
        }





        // Patient Details 
        [HttpGet("patient/details")]
        public async Task<IActionResult> GetPatientDetailsByHfId([FromQuery] string hfId)
        {
            if (string.IsNullOrWhiteSpace(hfId))
                return BadRequest(ApiResponseFactory.Fail("HFID is required."));

            var user = await _userRepository.GetUserByHFIDAsync(hfId);

            if (user == null)
            {
                _logger.LogInformation("No user found for HFID {HfId}", hfId);
                return Ok(ApiResponseFactory.Success(new PatientDetailsResponse(), "No patient found."));
            }

            var response = new PatientDetailsResponse
            {
                FullName = $"{user.FirstName} {user.LastName}".Trim(),
                Gender = user.Gender,
                DOB = user.DOB,
                HfId = user.HfId,
                PhoneNumber = user.PhoneNumber,
                City = user.City,
                State = user.State
            };

            _logger.LogInformation("Fetched patient details for HFID {HfId}", hfId);
            return Ok(ApiResponseFactory.Success(response, "Patient Details fetched successfully."));
        }
    }
}
