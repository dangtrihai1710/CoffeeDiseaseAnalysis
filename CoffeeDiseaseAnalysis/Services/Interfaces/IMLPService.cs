// File: CoffeeDiseaseAnalysis/Services/Interfaces/IMLPService.cs - FIXED
namespace CoffeeDiseaseAnalysis.Services.Interfaces
{
    public interface IMLPService
    {
        Task<decimal> PredictFromSymptomsAsync(List<int> symptomIds);
        Task<Dictionary<string, decimal>> PredictAllClassesFromSymptomsAsync(List<int> symptomIds);
        Task TrainMLPModelAsync();
        Task<bool> IsModelAvailableAsync();
    }
}