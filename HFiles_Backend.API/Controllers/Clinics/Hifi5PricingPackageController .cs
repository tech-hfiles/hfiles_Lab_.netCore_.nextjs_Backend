using HFiles_Backend.Application.Common;
using HFiles_Backend.Application.DTOs.Clinics.Hifi5_Packges;
using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HFiles_Backend.API.Controllers.Clinics
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class Hifi5PricingPackageController : ControllerBase
    {
        private readonly IHifi5PricingPackageRepository _repository;
        private readonly ILogger<Hifi5PricingPackageController> _logger;

        public Hifi5PricingPackageController(
            IHifi5PricingPackageRepository repository,
            ILogger<Hifi5PricingPackageController> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        /// <summary>
        /// Get pricing package by ID
        /// </summary>
        [HttpGet("PackageList")]
        public async Task<ActionResult<IEnumerable<Hifi5PricingPackageResponseDto>>> GetAll()
        {
            HttpContext.Items["Log-Category"] = "Pricing Package Management";
            _logger.LogInformation("Fetching all pricing packages");
            try
            {
                var packages = await _repository.GetAllAsync();
                if (packages == null || !packages.Any())
                {
                    _logger.LogWarning("No pricing packages found");
                    return Ok(ApiResponseFactory.Success(new List<Hifi5PricingPackageResponseDto>(), "No pricing packages found."));
                }

                // Sort packages in ascending order
                var sortedPackages = packages.OrderBy(p => p.Id).ToList(); // Change p.Id to your preferred sort field

                var responseDtos = sortedPackages.Select(p => MapToResponseDto(p)).ToList();
                _logger.LogInformation("Successfully retrieved {Count} pricing packages", responseDtos.Count);
                return Ok(ApiResponseFactory.Success(responseDtos, "Pricing packages retrieved successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving pricing packages");
                return StatusCode(500, ApiResponseFactory.Fail("An error occurred while retrieving pricing packages."));
            }
        }

        /// <summary>
        /// Get pricing packages by clinic ID
        /// </summary>
        [HttpGet("clinic/{clinicId:int}")]
        public async Task<ActionResult<IEnumerable<Hifi5PricingPackageResponseDto>>> GetByClinicId(int clinicId)
        {
            HttpContext.Items["Log-Category"] = "Pricing Package Management";
            _logger.LogInformation("Fetching pricing packages for clinic ID: {ClinicId}", clinicId);
            try
            {
                var packages = await _repository.GetByClinicIdAsync(clinicId);
                var response = packages
                    .OrderBy(p => p.Id) // Change this to whatever property you want to sort by
                    .Select(MapToResponseDto)
                    .ToList();

                if (!response.Any())
                {
                    _logger.LogInformation("No pricing packages found for clinic ID: {ClinicId}", clinicId);
                    return Ok(ApiResponseFactory.Success(new List<Hifi5PricingPackageResponseDto>(), $"No pricing packages found for clinic ID {clinicId}."));
                }
                _logger.LogInformation("Successfully retrieved {Count} pricing packages for clinic ID: {ClinicId}", response.Count, clinicId);
                return Ok(ApiResponseFactory.Success(response, $"Pricing packages for clinic ID {clinicId} retrieved successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving pricing packages for clinic ID: {ClinicId}", clinicId);
                return StatusCode(500, ApiResponseFactory.Fail("An error occurred while retrieving pricing packages."));
            }
        }

        /// <summary>
        /// Get pricing packages by program category
        /// </summary>
        [HttpGet("category/{category}")]
        public async Task<ActionResult<IEnumerable<Hifi5PricingPackageResponseDto>>> GetByCategory(string category)
        {
            HttpContext.Items["Log-Category"] = "Pricing Package Management";
            _logger.LogInformation("Fetching pricing packages for category: {Category}", category);

            try
            {
                var packages = await _repository.GetByProgramCategoryAsync(category);
                var response = packages.Select(MapToResponseDto).ToList();

                if (!response.Any())
                {
                    _logger.LogInformation("No pricing packages found for category: {Category}", category);
                    return Ok(ApiResponseFactory.Success(new List<Hifi5PricingPackageResponseDto>(), $"No pricing packages found for category '{category}'."));
                }

                _logger.LogInformation("Successfully retrieved {Count} pricing packages for category: {Category}", response.Count, category);
                return Ok(ApiResponseFactory.Success(response, $"Pricing packages for category '{category}' retrieved successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving pricing packages for category: {Category}", category);
                return StatusCode(500, ApiResponseFactory.Fail("An error occurred while retrieving pricing packages."));
            }
        }

        /// <summary>
        /// Search pricing packages by program name
        /// </summary>
        [HttpGet("search/{programName}")]
        public async Task<ActionResult<IEnumerable<Hifi5PricingPackageResponseDto>>> SearchByProgramName(string programName)
        {
            HttpContext.Items["Log-Category"] = "Pricing Package Management";
            _logger.LogInformation("Searching pricing packages with program name: {ProgramName}", programName);

            try
            {
                var packages = await _repository.GetByProgramNameAsync(programName);
                var response = packages.Select(MapToResponseDto).ToList();

                if (!response.Any())
                {
                    _logger.LogInformation("No pricing packages found matching program name: {ProgramName}", programName);
                    return Ok(ApiResponseFactory.Success(new List<Hifi5PricingPackageResponseDto>(), $"No pricing packages found matching '{programName}'."));
                }

                _logger.LogInformation("Successfully found {Count} pricing packages matching program name: {ProgramName}", response.Count, programName);
                return Ok(ApiResponseFactory.Success(response, $"Found {response.Count} pricing package(s) matching '{programName}'."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while searching pricing packages with program name: {ProgramName}", programName);
                return StatusCode(500, ApiResponseFactory.Fail("An error occurred while searching pricing packages."));
            }
        }


        [HttpGet("{id}")]
        public async Task<ActionResult<Hifi5PricingPackageResponseDto>> GetById(int id)
        {
            HttpContext.Items["Log-Category"] = "Pricing Package Management";
            _logger.LogInformation("Fetching pricing package with ID: {Id}", id);
            try
            {
                var package = await _repository.GetByIdAsync(id);
                if (package == null)
                {
                    _logger.LogWarning("Pricing package with ID {Id} not found", id);
                    return NotFound(ApiResponseFactory.Fail($"Pricing package with ID {id} not found."));
                }
                _logger.LogInformation("Successfully retrieved pricing package with ID: {Id}", id);
                return Ok(ApiResponseFactory.Success(MapToResponseDto(package), "Pricing package retrieved successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving pricing package with ID: {Id}", id);
                return StatusCode(500, ApiResponseFactory.Fail("An error occurred while retrieving the pricing package."));
            }
        }

        /// <summary>
        /// Update an existing pricing package
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Policy = "SuperAdminOrAdminPolicy")]
        public async Task<ActionResult<Hifi5PricingPackageResponseDto>> Update(int id, [FromBody] Hifi5PricingPackageRequestDto requestDto)
        {
            HttpContext.Items["Log-Category"] = "Pricing Package Management";
            _logger.LogInformation("Updating pricing package with ID: {Id}", id);

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                _logger.LogWarning("Validation failed while updating pricing package ID {Id}: {@Errors}", id, errors);
                return BadRequest(ApiResponseFactory.Fail(errors));
            }

            try
            {
                var exists = await _repository.ExistsAsync(id);
                if (!exists)
                {
                    _logger.LogWarning("Pricing package with ID {Id} not found for update", id);
                    return NotFound(ApiResponseFactory.Fail($"Pricing package with ID {id} not found."));
                }

                var package = MapToEntity(requestDto);
                package.Id = id;

                var updatedPackage = await _repository.UpdateAsync(package);
                var responseData = MapToResponseDto(updatedPackage);

                _logger.LogInformation("Successfully updated pricing package with ID: {Id}", id);
                return Ok(ApiResponseFactory.Success(responseData, "Pricing package updated successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while updating pricing package with ID: {Id}", id);
                return StatusCode(500, ApiResponseFactory.Fail("An error occurred while updating the pricing package."));
            }
        }

        /// <summary>
        /// Delete a pricing package
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Policy = "SuperAdminPolicy")]
        public async Task<ActionResult> Delete(int id)
        {
            HttpContext.Items["Log-Category"] = "Pricing Package Management";
            _logger.LogInformation("Deleting pricing package with ID: {Id}", id);

            try
            {
                var package = await _repository.GetByIdAsync(id);
                if (package == null)
                {
                    _logger.LogWarning("Pricing package with ID {Id} not found for deletion", id);
                    return NotFound(ApiResponseFactory.Fail($"Pricing package with ID {id} not found."));
                }

                var result = await _repository.DeleteAsync(id);
                if (!result)
                {
                    _logger.LogWarning("Failed to delete pricing package with ID {Id}", id);
                    return StatusCode(500, ApiResponseFactory.Fail("Failed to delete pricing package."));
                }

                _logger.LogInformation("Successfully deleted pricing package with ID: {Id}, Program: {ProgramName}",
                    id, package.ProgramName);

                return Ok(ApiResponseFactory.Success(new { DeletedId = id, ProgramName = package.ProgramName },
                    "Pricing package deleted successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while deleting pricing package with ID: {Id}", id);
                return StatusCode(500, ApiResponseFactory.Fail("An error occurred while deleting the pricing package."));
            }
        }

        // Mapping methods
        private Hifi5PricingPackageResponseDto MapToResponseDto(Hifi5PricingPackage package)
        {
            return new Hifi5PricingPackageResponseDto
            {
                Id = package.Id,
                ClinicId = package.ClinicId,
                ProgramCategory = package.ProgramCategory,
                ProgramName = package.ProgramName,
                DurationMonths = package.DurationMonths,
                Frequency = package.Frequency,
                TotalSessions = package.TotalSessions,
                PriceInr = package.PriceInr,
                IncludesPhysio = package.IncludesPhysio,
                PhysioSessions = package.PhysioSessions,
                ExtensionAllowed = package.ExtensionAllowed,
                EpochTime = package.EpochTime
            };
        }

        private Hifi5PricingPackage MapToEntity(Hifi5PricingPackageRequestDto dto)
        {
            return new Hifi5PricingPackage
            {
                ClinicId = dto.ClinicId,
                ProgramCategory = dto.ProgramCategory,
                ProgramName = dto.ProgramName,
                DurationMonths = dto.DurationMonths,
                Frequency = dto.Frequency,
                TotalSessions = dto.TotalSessions,
                PriceInr = dto.PriceInr,
                IncludesPhysio = dto.IncludesPhysio,
                PhysioSessions = dto.PhysioSessions,
                ExtensionAllowed = dto.ExtensionAllowed,
                EpochTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
        }
    }
}