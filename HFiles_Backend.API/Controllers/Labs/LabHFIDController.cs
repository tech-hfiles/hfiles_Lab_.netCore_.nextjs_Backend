using System.ComponentModel.DataAnnotations;
using HFiles_Backend.API.Services;
using HFiles_Backend.Application.Common;
using HFiles_Backend.Application.DTOs.Labs;
using HFiles_Backend.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace HFiles_Backend.API.Controllers.Labs

{
    [ApiController]
    [Route("api")]
    public class LabHFIDController(AppDbContext context, ILogger<LabHFIDController> logger) : ControllerBase
    {
        private readonly AppDbContext _context = context;
        private readonly ILogger<LabHFIDController> _logger = logger;





        // Verify HFID for Labs
        [HttpGet("labs/hfid")]
        public async Task<IActionResult> GetHFIDByEmail([FromQuery][Required, EmailAddress, MaxLength(100)] string email)
        {
            HttpContext.Items["Log-Category"] = "Identity Verification";
            _logger.LogInformation("Received request to fetch HFID for Email: {Email}", email);

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors)
                                              .Select(e => e.ErrorMessage).ToList();

                _logger.LogWarning("Validation failed: {@Errors}", errors);
                return BadRequest(ApiResponseFactory.Fail(errors));
            }

            try
            {
                await using var tx = await _context.Database.BeginTransactionAsync().ConfigureAwait(false);

                var lab = await _context.LabSignups
                    .Where(u => u.Email == email)
                    .Select(u => new { u.Email, u.LabName, u.HFID, u.ProfilePhoto })
                    .FirstOrDefaultAsync()
                    .ConfigureAwait(false);

                if (lab == null)
                {
                    _logger.LogWarning("HFID retrieval failed: Lab with Email {Email} not found.", email);
                    return NotFound(ApiResponseFactory.Fail($"Lab with email '{email}' not found."));
                }

                if (string.IsNullOrWhiteSpace(lab.HFID))
                {
                    _logger.LogWarning("HFID retrieval failed: HFID missing for Email {Email}.", email);
                    return NotFound(ApiResponseFactory.Fail("HFID has not been generated yet for this user."));
                }

                await tx.CommitAsync().ConfigureAwait(false);

                _logger.LogInformation("HFID retrieval successful for Email {Email}.", email);
                return Ok(ApiResponseFactory.Success(lab, "HFID retrieved successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HFID retrieval failed due to an unexpected error for Email {Email}", email);
                return StatusCode(500, ApiResponseFactory.Fail($"An unexpected error occurred: {ex.Message}"));
            }
        }





        // Verify HFID for Users
            [HttpPost("users/hfid")]
            public async Task<IActionResult> GetUserDetails([FromBody] HFIDRequest dto)
            {
                HttpContext.Items["Log-Category"] = "Identity Verification";
                _logger.LogInformation("Received request to fetch user details for HFID: {HFID}", dto?.HFID);

                // Check if dto is null
                if (dto == null || string.IsNullOrWhiteSpace(dto.HFID))
                {
                    _logger.LogWarning("Invalid request: HFID is required");
                    return BadRequest(ApiResponseFactory.Fail("HFID is required"));
                }

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                    _logger.LogWarning("Validation failed: {@Errors}", errors);
                    return BadRequest(ApiResponseFactory.Fail(errors));
                }

                try
                {
                    var userDetails = await _context.Users
                        .Where(u => u.HfId == dto.HFID)
                        .Select(u => new
                        {
                            id = u.Id,
                            Username = $"{u.FirstName} {u.LastName}",
                            UserEmail = u.Email,
                            UserProfile = u.ProfilePhoto,
                            DOB = u.DOB,
                            Gender = u.Gender,
                            Phone = u.PhoneNumber

                        })
                        .FirstOrDefaultAsync();

                    if (userDetails == null)
                    {
                        _logger.LogWarning("User details retrieval failed: No user found with HFID {HFID}", dto.HFID);
                        return NotFound(ApiResponseFactory.Fail($"No user found with HFID '{dto.HFID}'"));
                    }

                    _logger.LogInformation("Successfully fetched user details for HFID {HFID}.", dto.HFID);
                    return Ok(ApiResponseFactory.Success(userDetails, "User details retrieved successfully."));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "User details retrieval failed due to an unexpected error for HFID {HFID}", dto.HFID);
                    return StatusCode(500, ApiResponseFactory.Fail($"An unexpected error occurred: {ex.Message}"));
                }
            }
        }
}
