// ==========================================
// 7. Services/Interfaces/IModelManagementService.cs - MISSING SERVICE
// ==========================================
namespace CoffeeDiseaseAnalysis.Services.Interfaces
{
    public interface IModelManagementService
    {
        Task<object> GetActiveModelAsync();
        Task<bool> SwitchModelAsync(int modelId);
        Task<object> GetModelPerformanceAsync(int modelId);
        Task<bool> ValidateModelAsync(string modelPath);
        Task<bool> IsHealthyAsync();
    }
}
