// ==========================================
// CoffeeDiseaseAnalysis/Services/OtpService.cs - NEW SERVICE
// ==========================================
using CoffeeDiseaseAnalysis.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;
using System.Text;

namespace CoffeeDiseaseAnalysis.Services
{
    public class OtpService : IOtpService
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<OtpService> _logger;
        private readonly IConfiguration _configuration;

        // Cấu hình OTP
        private readonly int _otpLength;
        private readonly int _otpExpiryMinutes;
        private readonly int _maxAttempts;

        public OtpService(IMemoryCache cache, ILogger<OtpService> logger, IConfiguration configuration)
        {
            _cache = cache;
            _logger = logger;
            _configuration = configuration;

            // Đọc cấu hình từ appsettings.json
            _otpLength = int.Parse(_configuration["OtpSettings:Length"] ?? "6");
            _otpExpiryMinutes = int.Parse(_configuration["OtpSettings:ExpiryMinutes"] ?? "5");
            _maxAttempts = int.Parse(_configuration["OtpSettings:MaxAttempts"] ?? "3");
        }

        public string GenerateOtp(string email)
        {
            try
            {
                // Tạo OTP ngẫu nhiên
                var otp = GenerateRandomOtp();

                // Tạo key cho cache
                var cacheKey = GetOtpCacheKey(email);

                // Lưu OTP vào cache với thời gian hết hạn
                var otpData = new OtpData
                {
                    Code = otp,
                    Email = email,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(_otpExpiryMinutes),
                    Attempts = 0,
                    IsUsed = false
                };

                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_otpExpiryMinutes),
                    Priority = CacheItemPriority.High
                };

                _cache.Set(cacheKey, otpData, cacheOptions);

                _logger.LogInformation("OTP generated for email: {Email}, expires at: {ExpiresAt}",
                    email, otpData.ExpiresAt);

                return otp;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate OTP for email: {Email}", email);
                throw new InvalidOperationException("Failed to generate OTP", ex);
            }
        }

        public bool ValidateOtp(string email, string otp)
        {
            try
            {
                var cacheKey = GetOtpCacheKey(email);

                if (!_cache.TryGetValue(cacheKey, out OtpData? otpData) || otpData == null)
                {
                    _logger.LogWarning("OTP not found or expired for email: {Email}", email);
                    return false;
                }

                // Kiểm tra số lần thử
                otpData.Attempts++;

                if (otpData.Attempts > _maxAttempts)
                {
                    _logger.LogWarning("Too many OTP attempts for email: {Email}", email);
                    _cache.Remove(cacheKey);
                    return false;
                }

                // Kiểm tra OTP đã được sử dụng chưa
                if (otpData.IsUsed)
                {
                    _logger.LogWarning("OTP already used for email: {Email}", email);
                    return false;
                }

                // Kiểm tra hết hạn
                if (DateTime.UtcNow > otpData.ExpiresAt)
                {
                    _logger.LogWarning("OTP expired for email: {Email}", email);
                    _cache.Remove(cacheKey);
                    return false;
                }

                // Kiểm tra mã OTP
                if (otpData.Code != otp)
                {
                    _logger.LogWarning("Invalid OTP for email: {Email}, attempts: {Attempts}",
                        email, otpData.Attempts);

                    // Cập nhật số lần thử
                    _cache.Set(cacheKey, otpData, TimeSpan.FromMinutes(_otpExpiryMinutes));
                    return false;
                }

                // OTP hợp lệ - đánh dấu đã sử dụng
                otpData.IsUsed = true;
                _cache.Set(cacheKey, otpData, TimeSpan.FromMinutes(_otpExpiryMinutes));

                _logger.LogInformation("OTP validated successfully for email: {Email}", email);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to validate OTP for email: {Email}", email);
                return false;
            }
        }

        public void InvalidateOtp(string email)
        {
            try
            {
                var cacheKey = GetOtpCacheKey(email);
                _cache.Remove(cacheKey);
                _logger.LogInformation("OTP invalidated for email: {Email}", email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to invalidate OTP for email: {Email}", email);
            }
        }

        public bool IsOtpValid(string email)
        {
            try
            {
                var cacheKey = GetOtpCacheKey(email);

                if (!_cache.TryGetValue(cacheKey, out OtpData? otpData) || otpData == null)
                {
                    return false;
                }

                return !otpData.IsUsed && DateTime.UtcNow <= otpData.ExpiresAt;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check OTP validity for email: {Email}", email);
                return false;
            }
        }

        public TimeSpan? GetOtpTimeRemaining(string email)
        {
            try
            {
                var cacheKey = GetOtpCacheKey(email);

                if (!_cache.TryGetValue(cacheKey, out OtpData? otpData) || otpData == null)
                {
                    return null;
                }

                var remaining = otpData.ExpiresAt - DateTime.UtcNow;
                return remaining.TotalSeconds > 0 ? remaining : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get OTP time remaining for email: {Email}", email);
                return null;
            }
        }

        #region Private Methods

        private string GenerateRandomOtp()
        {
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[4];
            rng.GetBytes(bytes);

            var number = Math.Abs(BitConverter.ToInt32(bytes, 0));
            var otp = (number % (int)Math.Pow(10, _otpLength)).ToString($"D{_otpLength}");

            return otp;
        }

        private string GetOtpCacheKey(string email)
        {
            return $"otp_{email.ToLowerInvariant()}";
        }

        #endregion
    }

    // ==========================================
    // DATA MODEL
    // ==========================================
    public class OtpData
    {
        public string Code { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public int Attempts { get; set; }
        public bool IsUsed { get; set; }
    }
}

