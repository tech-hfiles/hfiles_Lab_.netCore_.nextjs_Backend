using Microsoft.Extensions.Caching.Memory;

namespace HFiles_Backend.API.Services
{
    public class OtpVerificationStore(IMemoryCache cache)
    {
        private readonly IMemoryCache _cache = cache;
        private const string KeyPrefix = "OTP_VERIFIED:";

        private string GetCacheKey(string email, string purpose) => $"{KeyPrefix}{email}:{purpose}";

        public void StoreVerifiedOtp(string email, string purpose, TimeSpan? expiration = null)
        {
            var entry = new OtpVerificationEntry
            {
                Email = email,
                Purpose = purpose,
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            var key = GetCacheKey(email, purpose);
            Console.WriteLine($"[OTP Store] Storing OTP for Key: {key}");
            _cache.Set(key, entry, expiration ?? TimeSpan.FromMinutes(10));
        }

        public bool IsVerified(string email, string purpose)
        {
            return _cache.TryGetValue<OtpVerificationEntry>(GetCacheKey(email, purpose), out _);
        }

        public bool Consume(string email, string purpose)
        {
            var key = GetCacheKey(email, purpose);
            var result = _cache.TryGetValue<OtpVerificationEntry>(key, out var _);
            Console.WriteLine($"[OTP Store] Consume called. Key: {key}, Found: {result}");
            if (result)
            {
                _cache.Remove(key);
                return true;
            }
            return false;
        }

        private class OtpVerificationEntry
        {
            public string Email { get; set; } = null!;
            public string Purpose { get; set; } = null!;
            public long CreatedAt { get; set; }
        }
    }
}
