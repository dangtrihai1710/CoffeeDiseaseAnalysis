// File: CoffeeDiseaseAnalysis/Services/Interfaces/IPredictionService.cs
using CoffeeDiseaseAnalysis.Models.DTOs;

namespace CoffeeDiseaseAnalysis.Services.Interfaces
{
    public interface IPredictionService
    {
        Task<PredictionResult> PredictDiseaseAsync(byte[] imageBytes, string imagePath, List<int>? symptomIds = null);
        Task<bool> IsModelAvailableAsync();
        Task<Dictionary<string, object>> GetModelStatsAsync();
    }
}