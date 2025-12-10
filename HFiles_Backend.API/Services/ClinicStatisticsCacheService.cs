using Microsoft.Extensions.Caching.Memory;

namespace HFiles_Backend.API.Services
{
    public interface IClinicStatisticsCacheService
    {
        void InvalidateClinicStatistics(int clinicId);
        void InvalidateAllClinicStatistics();
        void TrackCacheKey(string key);
    }

    public class ClinicStatisticsCacheService(
        IMemoryCache cache,
        ILogger<ClinicStatisticsCacheService> logger) : IClinicStatisticsCacheService
    {
        private readonly IMemoryCache _cache = cache;
        private readonly ILogger<ClinicStatisticsCacheService> _logger = logger;
        private static readonly HashSet<string> _cacheKeys = new();
        private static readonly object _lock = new();

        public void InvalidateClinicStatistics(int clinicId)
        {
            lock (_lock)
            {
                // Find all cache keys for this clinic
                var keysToRemove = _cacheKeys
                    .Where(k => k.StartsWith($"clinic_stats_{clinicId}_"))
                    .ToList();

                foreach (var key in keysToRemove)
                {
                    _cache.Remove(key);
                    _cacheKeys.Remove(key);
                }

                _logger.LogInformation("Invalidated {Count} cache entries for Clinic ID {ClinicId}",
                    keysToRemove.Count, clinicId);
            }
        }

        public void InvalidateAllClinicStatistics()
        {
            lock (_lock)
            {
                foreach (var key in _cacheKeys)
                {
                    _cache.Remove(key);
                }
                _cacheKeys.Clear();
                _logger.LogInformation("Invalidated all clinic statistics cache entries");
            }
        }

        public void TrackCacheKey(string key)
        {
            lock (_lock)
            {
                _cacheKeys.Add(key);
            }
        }
    }
}
