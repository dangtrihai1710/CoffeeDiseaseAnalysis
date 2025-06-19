// ==========================================
// File: CoffeeDiseaseAnalysis/Services/Interfaces/IModelTrainingBackgroundService.cs
// ==========================================
namespace CoffeeDiseaseAnalysis.Services.Interfaces
{
    public interface IModelTrainingBackgroundService
    {
        Task StartTrainingAsync();
        Task StopTrainingAsync();
        Task<bool> IsTrainingAsync();
    }
}