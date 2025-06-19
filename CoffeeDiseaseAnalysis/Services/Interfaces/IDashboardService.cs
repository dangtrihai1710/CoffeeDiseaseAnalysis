
// ==========================================
// File: CoffeeDiseaseAnalysis/Services/Interfaces/IDashboardService.cs
// ==========================================
namespace CoffeeDiseaseAnalysis.Services.Interfaces
{
    public interface IDashboardService
    {
        Task<object> GetOverviewAsync();
        Task<object> GetStatsAsync();
        Task<object> GetPerformanceMetricsAsync();
        Task<bool> IsHealthyAsync();
    }
}