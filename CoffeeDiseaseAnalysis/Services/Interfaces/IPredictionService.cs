// File: CoffeeDiseaseAnalysis/Services/Interfaces/IPredictionService.cs - COMPLETE
using CoffeeDiseaseAnalysis.Models.DTOs;

namespace CoffeeDiseaseAnalysis.Services.Interfaces
{
    public interface IPredictionService
    {
        Task<PredictionResult> PredictDiseaseAsync(byte[] imageBytes, string imagePath, List<int>? symptomIds = null);
        Task<bool> IsModelAvailableAsync();
        Task<ModelStatistics> GetModelStatsAsync();
        Task<BatchPredictionResponse> PredictBatchAsync(List<byte[]> imageBytes, List<string> imagePaths);
    }
}