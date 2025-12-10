using HFiles_Backend.Domain.Interfaces;

namespace HFiles_Backend.API.Services
{
    /// <summary>
    /// Background service that automatically refreshes expired Google OAuth tokens
    /// Runs every 30 minutes to check and refresh tokens
    /// </summary>
    public class GoogleTokenRefreshService : BackgroundService
    {
        private readonly ILogger<GoogleTokenRefreshService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(30);

        public GoogleTokenRefreshService(
            ILogger<GoogleTokenRefreshService> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Google Token Refresh Service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RefreshExpiredTokensAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in Google Token Refresh Service");
                }

                // Wait for next check interval
                await Task.Delay(_checkInterval, stoppingToken);
            }

            _logger.LogInformation("Google Token Refresh Service stopped");
        }

        private async Task RefreshExpiredTokensAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var tokenRepository = scope.ServiceProvider.GetRequiredService<IClinicGoogleTokenRepository>();
            var googleAuthService = scope.ServiceProvider.GetRequiredService<IGoogleAuthService>();

            try
            {
                // Get all tokens that need refresh (expired or expiring in next 10 minutes)
                var tokensToRefresh = await tokenRepository.GetTokensNeedingRefreshAsync();

                if (!tokensToRefresh.Any())
                {
                    _logger.LogDebug("No tokens need refreshing at this time");
                    return;
                }

                _logger.LogInformation("Found {Count} tokens needing refresh", tokensToRefresh.Count);

                foreach (var token in tokensToRefresh)
                {
                    try
                    {
                        var success = await googleAuthService.RefreshAccessTokenAsync(token.ClinicId);

                        if (success)
                        {
                            _logger.LogInformation("Successfully refreshed token for Clinic ID {ClinicId}", token.ClinicId);
                        }
                        else
                        {
                            _logger.LogWarning("Failed to refresh token for Clinic ID {ClinicId}", token.ClinicId);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error refreshing token for Clinic ID {ClinicId}", token.ClinicId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in RefreshExpiredTokensAsync");
            }
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Google Token Refresh Service is stopping");
            await base.StopAsync(stoppingToken);
        }
    }
}
