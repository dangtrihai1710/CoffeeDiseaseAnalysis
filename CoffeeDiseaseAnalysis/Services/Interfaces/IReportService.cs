
// File: CoffeeDiseaseAnalysis/Services/Interfaces/IReportService.cs
namespace CoffeeDiseaseAnalysis.Services.Interfaces
{
    public interface IReportService
    {
        Task<object> GenerateUserReportAsync(string userId);
        Task<object> GenerateSystemReportAsync();
    }
}