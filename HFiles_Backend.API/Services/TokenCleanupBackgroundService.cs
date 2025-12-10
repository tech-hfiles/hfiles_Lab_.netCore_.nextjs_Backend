using HFiles_Backend.API.Interfaces;

namespace HFiles_Backend.API.Services
{
    public class TokenCleanupBackgroundService(IServiceProvider serviceProvider, ILogger<TokenCleanupBackgroundService> logger) : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider = serviceProvider;
        private readonly ILogger<TokenCleanupBackgroundService> _logger = logger;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var tokenBlacklistService = scope.ServiceProvider.GetRequiredService<ITokenBlacklistService>();
                    await tokenBlacklistService.CleanupExpiredTokensAsync();
                    _logger.LogInformation("Successfully cleaned up expired tokens.");
                    await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during token cleanup");
                    await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken); // Wait 2 minutes before retrying
                }
            }
        }
    }
}
