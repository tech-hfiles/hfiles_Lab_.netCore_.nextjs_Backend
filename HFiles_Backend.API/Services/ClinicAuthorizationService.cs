using HFiles_Backend.API.Interfaces;
using HFiles_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HFiles_Backend.API.Services
{
    public class ClinicAuthorizationService : IClinicAuthorizationService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ClinicAuthorizationService> _logger;

        public ClinicAuthorizationService(AppDbContext context, ILogger<ClinicAuthorizationService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _logger.LogInformation("ClinicAuthorizationService initialized successfully.");
        }

        public async Task<bool> IsClinicAuthorized(int clinicId, ClaimsPrincipal user)
        {
            _logger.LogInformation("Checking authorization for Clinic ID: {ClinicId}.", clinicId);

            var userIdClaim = user.Claims.FirstOrDefault(c => c.Type == "UserId");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                _logger.LogWarning("Authorization failed: Missing or invalid UserId claim.");
                return false;
            }

            _logger.LogInformation("User ID {UserId} is attempting authorization for Clinic ID {ClinicId}.", userId, clinicId);

            var loggedInClinic = await _context.ClinicSignups.FirstOrDefaultAsync(c => c.Id == userId);
            if (loggedInClinic == null)
            {
                _logger.LogWarning("Authorization failed: Clinic ID {UserId} not found.", userId);
                return false;
            }

            if (loggedInClinic.Id != clinicId)
            {
                return false;
            }
            int mainClinicId = loggedInClinic.ClinicReference == 0 ? userId : loggedInClinic.ClinicReference;

            _logger.LogInformation("Resolved Main Clinic ID: {MainClinicId}. Fetching associated branches.", mainClinicId);

            var branchIds = await _context.ClinicSignups
                .Where(c => c.ClinicReference == mainClinicId)
                .Select(c => c.Id)
                .ToListAsync();

            branchIds.Add(mainClinicId);

            bool isAuthorized = branchIds.Contains(clinicId);

            _logger.LogInformation("Authorization result for Clinic ID {ClinicId}: {IsAuthorized}", clinicId, isAuthorized);

            return isAuthorized;
        }
    }
}
