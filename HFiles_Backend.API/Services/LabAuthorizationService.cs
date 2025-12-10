using System.Security.Claims;
using HFiles_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HFiles_Backend.API.Services
{
    public class LabAuthorizationService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<LabAuthorizationService> _logger;

        public LabAuthorizationService(AppDbContext context, ILogger<LabAuthorizationService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _logger.LogInformation("LabAuthorizationService initialized successfully.");
        }

        public async Task<bool> IsLabAuthorized(int labId, ClaimsPrincipal user)
        {
            _logger.LogInformation("Checking authorization for Lab ID: {LabId}.", labId);

            var labIdClaim = user.Claims.FirstOrDefault(c => c.Type == "UserId");
            if (labIdClaim == null || !int.TryParse(labIdClaim.Value, out int userLabId))
            {
                _logger.LogWarning("Authorization failed: Missing or invalid UserId claim.");
                return false;
            }

            _logger.LogInformation("User ID {UserLabId} is attempting authorization for Lab ID {LabId}.", userLabId, labId);

            var loggedInLab = await _context.LabSignups.FirstOrDefaultAsync(l => l.Id == userLabId);
            if (loggedInLab == null)
            {
                _logger.LogWarning("Authorization failed: Lab ID {UserLabId} not found.", userLabId);
                return false;
            }

            int mainLabId = loggedInLab.LabReference == 0 ? userLabId : loggedInLab.LabReference;

            _logger.LogInformation("Resolved Main Lab ID: {MainLabId}. Fetching associated branches.", mainLabId);

            var branchIds = await _context.LabSignups
                .Where(l => l.LabReference == mainLabId)
                .Select(l => l.Id)
                .ToListAsync();

            branchIds.Add(mainLabId);

            bool isAuthorized = branchIds.Contains(labId);

            _logger.LogInformation("Authorization result for Lab ID {LabId}: {IsAuthorized}", labId, isAuthorized);

            return isAuthorized;
        }
    }
}
