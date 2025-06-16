using CoffeeDiseaseAnalysis.Models.DTOs;

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