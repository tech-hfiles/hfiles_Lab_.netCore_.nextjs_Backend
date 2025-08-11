using HFiles_Backend.Application.Common;
using HFiles_Backend.Domain.Interfaces;
using HFiles_Backend.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Storage;
using System.ComponentModel.DataAnnotations;

namespace HFiles_Backend.API.Controllers.Clinics
{
    [Route("api/")]
    [ApiController]
    public class ClinicHFIDController(
        ILogger<ClinicHFIDController> logger,
         IClinicRepository clinicRepository
        ) : ControllerBase
    {
        private readonly ILogger<ClinicHFIDController> _logger = logger;
        private readonly IClinicRepository _clinicRepository = clinicRepository;





        // Verify HFID for Clinic
        [HttpGet("clinics/hfid")]
        public async Task<IActionResult> GetClinicHFIDByEmail([FromQuery][Required, EmailAddress, MaxLength(100)] string email, [FromServices] ClinicRepository clinicRepository)
        {
            HttpContext.Items["Log-Category"] = "Identity Verification";
            _logger.LogInformation("Received request to fetch HFID for Clinic Email: {Email}", email);

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors)
                                              .Select(e => e.ErrorMessage).ToList();

                _logger.LogWarning("Validation failed: {@Errors}", errors);
                return BadRequest(ApiResponseFactory.Fail(errors));
            }

            var transaction = await _clinicRepository.BeginTransactionAsync();

            try
            {
                var clinicInfo = await clinicRepository.GetHFIDByEmailAsync(email);
                if (clinicInfo == null)
                {
                    _logger.LogWarning("HFID retrieval failed: Clinic with Email {Email} not found.", email);
                    return NotFound(ApiResponseFactory.Fail($"Clinic with email '{email}' not found."));
                }

                if (string.IsNullOrWhiteSpace(clinicInfo.HFID))
                {
                    _logger.LogWarning("HFID retrieval failed: HFID missing for Email {Email}.", email);
                    return NotFound(ApiResponseFactory.Fail("HFID has not been generated yet for this clinic."));
                }

                await transaction.CommitAsync();
                _logger.LogInformation("HFID retrieval successful for Email {Email}.", email);
                return Ok(ApiResponseFactory.Success(clinicInfo, "HFID retrieved successfully."));
            }
            finally
            {
                if (transaction.GetDbTransaction().Connection != null)
                {
                    await transaction.RollbackAsync();
                }
            }
        }
    }
}
