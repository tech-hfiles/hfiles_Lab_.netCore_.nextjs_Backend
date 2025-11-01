using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Interfaces;
using HFiles_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HFiles_Backend.Infrastructure.Repositories
{
    public class ClinicGoogleTokenRepository : IClinicGoogleTokenRepository
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ClinicGoogleTokenRepository> _logger;

        public ClinicGoogleTokenRepository(
            AppDbContext context,
            ILogger<ClinicGoogleTokenRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<ClinicGoogleToken?> GetActiveTokenByClinicIdAsync(int clinicId)
        {
            try
            {
                return await _context.ClinicGoogleTokens
                    .Where(t => t.ClinicId == clinicId && t.IsActive)
                    .OrderByDescending(t => t.CreatedAt)
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving Google token for Clinic ID {ClinicId}", clinicId);
                throw;
            }
        }

        public async Task SaveTokenAsync(ClinicGoogleToken token)
        {
            try
            {
                // Deactivate any existing tokens for this clinic
                var existingTokens = await _context.ClinicGoogleTokens
                    .Where(t => t.ClinicId == token.ClinicId && t.IsActive)
                    .ToListAsync();

                foreach (var existingToken in existingTokens)
                {
                    existingToken.IsActive = false;
                    existingToken.UpdatedAt = DateTime.UtcNow;
                }

                // Add new token
                token.CreatedAt = DateTime.UtcNow;
                token.UpdatedAt = DateTime.UtcNow;
                token.IsActive = true;

                await _context.ClinicGoogleTokens.AddAsync(token);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Saved new Google token for Clinic ID {ClinicId}", token.ClinicId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving Google token for Clinic ID {ClinicId}", token.ClinicId);
                throw;
            }
        }

        public async Task UpdateTokenAsync(ClinicGoogleToken token)
        {
            try
            {
                token.UpdatedAt = DateTime.UtcNow;
                token.LastRefreshedAt = DateTime.UtcNow;

                _context.ClinicGoogleTokens.Update(token);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Updated Google token for Clinic ID {ClinicId}", token.ClinicId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating Google token for Clinic ID {ClinicId}", token.ClinicId);
                throw;
            }
        }

        public async Task<bool> HasValidTokenAsync(int clinicId)
        {
            try
            {
                var token = await GetActiveTokenByClinicIdAsync(clinicId);
                return token != null && token.TokenExpiry > DateTime.UtcNow.AddMinutes(5);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking token validity for Clinic ID {ClinicId}", clinicId);
                return false;
            }
        }

        public async Task RevokeTokenAsync(int clinicId)
        {
            try
            {
                var tokens = await _context.ClinicGoogleTokens
                    .Where(t => t.ClinicId == clinicId && t.IsActive)
                    .ToListAsync();

                foreach (var token in tokens)
                {
                    token.IsActive = false;
                    token.UpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Revoked Google tokens for Clinic ID {ClinicId}", clinicId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking tokens for Clinic ID {ClinicId}", clinicId);
                throw;
            }
        }

        public async Task<List<ClinicGoogleToken>> GetTokensNeedingRefreshAsync()
        {
            try
            {
                // Get tokens that expire in next 10 minutes
                var cutoffTime = DateTime.UtcNow.AddMinutes(10);

                return await _context.ClinicGoogleTokens
                    .Where(t => t.IsActive && t.TokenExpiry <= cutoffTime)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving tokens needing refresh");
                throw;
            }
        }
    }
}
