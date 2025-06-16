// File: CoffeeDiseaseAnalysis/Services/Interfaces/ICacheService.cs
using CoffeeDiseaseAnalysis.Models.DTOs;
using CoffeeDiseaseAnalysis.Services.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace CoffeeDiseaseAnalysis.Services.Interfaces
{
    public interface ICacheService
    {
        Task<PredictionResult?> GetPredictionAsync(string imageHash);
        Task SetPredictionAsync(string imageHash, PredictionResult result, TimeSpan expiry);
        Task<ModelStatistics?> GetModelStatsAsync(string modelVersion);
        Task SetModelStatsAsync(string modelVersion, ModelStatistics stats, TimeSpan expiry);
        Task InvalidatePredictionCacheAsync(string imageHash);
        Task InvalidateModelCacheAsync(string modelVersion);
        Task<bool> IsHealthyAsync();
    }
}

// File: CoffeeDiseaseAnalysis/Services/CacheService.cs
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using CoffeeDiseaseAnalysis.Services.Interfaces;
using CoffeeDiseaseAnalysis.Models.DTOs;
using System.Text.Json;

namespace CoffeeDiseaseAnalysis.Services
{
    public class CacheService : ICacheService
    {
        private readonly IDistributedCache _distributedCache;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<CacheService> _logger;

        private const string PREDICTION_PREFIX = "pred:";
        private const string MODEL_STATS_PREFIX = "model:";

        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        public CacheService(
            IDistributedCache distributedCache,
            IMemoryCache memoryCache,
            ILogger<CacheService> logger)
        {
            _distributedCache = distributedCache;
            _memoryCache = memoryCache;
            _logger = logger;
        }

        public async Task<PredictionResult?> GetPredictionAsync(string imageHash)
        {
            try
            {
                var key = PREDICTION_PREFIX + imageHash;

                // Thử memory cache trước (nhanh nhất)
                if (_memoryCache.TryGetValue(key, out PredictionResult? memResult))
                {
                    _logger.LogDebug("Memory cache hit for prediction: {Hash}", imageHash);
                    return memResult;
                }

                // Thử distributed cache (Redis)
                var cachedJson = await _distributedCache.GetStringAsync(key);
                if (!string.IsNullOrEmpty(cachedJson))
                {
                    var result = JsonSerializer.Deserialize<PredictionResult>(cachedJson, _jsonOptions);

                    // Lưu lại vào memory cache cho lần sau
                    _memoryCache.Set(key, result, TimeSpan.FromMinutes(30));

                    _logger.LogDebug("Distributed cache hit for prediction: {Hash}", imageHash);
                    return result;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi khi đọc cache cho prediction: {Hash}", imageHash);
                return null;
            }
        }

        public async Task SetPredictionAsync(string imageHash, PredictionResult result, TimeSpan expiry)
        {
            try
            {
                var key = PREDICTION_PREFIX + imageHash;
                var json = JsonSerializer.Serialize(result, _jsonOptions);

                var options = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = expiry
                };

                // Lưu vào distributed cache
                await _distributedCache.SetStringAsync(key, json, options);

                // Lưu vào memory cache với thời gian ngắn hơn
                var memoryExpiry = expiry > TimeSpan.FromHours(1) ? TimeSpan.FromHours(1) : expiry;
                _memoryCache.Set(key, result, memoryExpiry);

                _logger.LogDebug("Cached prediction for: {Hash}, expiry: {Expiry}", imageHash, expiry);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi khi lưu cache cho prediction: {Hash}", imageHash);
            }
        }

        public async Task<ModelStatistics?> GetModelStatsAsync(string modelVersion)
        {
            try
            {
                var key = MODEL_STATS_PREFIX + modelVersion;

                if (_memoryCache.TryGetValue(key, out ModelStatistics? memResult))
                {
                    return memResult;
                }

                var cachedJson = await _distributedCache.GetStringAsync(key);
                if (!string.IsNullOrEmpty(cachedJson))
                {
                    var result = JsonSerializer.Deserialize<ModelStatistics>(cachedJson, _jsonOptions);
                    _memoryCache.Set(key, result, TimeSpan.FromMinutes(15));
                    return result;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi khi đọc cache cho model stats: {Version}", modelVersion);
                return null;
            }
        }

        public async Task SetModelStatsAsync(string modelVersion, ModelStatistics stats, TimeSpan expiry)
        {
            try
            {
                var key = MODEL_STATS_PREFIX + modelVersion;
                var json = JsonSerializer.Serialize(stats, _jsonOptions);

                var options = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = expiry
                };

                await _distributedCache.SetStringAsync(key, json, options);
                _memoryCache.Set(key, stats, TimeSpan.FromMinutes(15));

                _logger.LogDebug("Cached model stats for: {Version}", modelVersion);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi khi lưu cache cho model stats: {Version}", modelVersion);
            }
        }

        public async Task InvalidatePredictionCacheAsync(string imageHash)
        {
            try
            {
                var key = PREDICTION_PREFIX + imageHash;
                await _distributedCache.RemoveAsync(key);
                _memoryCache.Remove(key);

                _logger.LogDebug("Invalidated prediction cache: {Hash}", imageHash);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi khi invalidate prediction cache: {Hash}", imageHash);
            }
        }

        public async Task InvalidateModelCacheAsync(string modelVersion)
        {
            try
            {
                var key = MODEL_STATS_PREFIX + modelVersion;
                await _distributedCache.RemoveAsync(key);
                _memoryCache.Remove(key);

                _logger.LogDebug("Invalidated model cache: {Version}", modelVersion);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi khi invalidate model cache: {Version}", modelVersion);
            }
        }

        public async Task<bool> IsHealthyAsync()
        {
            try
            {
                var testKey = "health_check_" + DateTime.UtcNow.Ticks;
                var testValue = "test";

                await _distributedCache.SetStringAsync(testKey, testValue, new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(10)
                });

                var retrieved = await _distributedCache.GetStringAsync(testKey);
                await _distributedCache.RemoveAsync(testKey);

                return retrieved == testValue;
            }
            catch
            {
                return false;
            }
        }
    }
}