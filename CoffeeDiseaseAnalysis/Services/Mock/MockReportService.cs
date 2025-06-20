// File: CoffeeDiseaseAnalysis/Services/Mock/MockReportService.cs
using CoffeeDiseaseAnalysis.Services.Interfaces;

namespace CoffeeDiseaseAnalysis.Services.Mock
{
    public class MockReportService : IReportService
    {
        private readonly ILogger<MockReportService> _logger;

        public MockReportService(ILogger<MockReportService> logger)
        {
            _logger = logger;
        }

        public async Task<object> GenerateUserReportAsync(string userId)
        {
            await Task.Delay(1000);
            _logger.LogInformation("Mock: Generated user report for {UserId}", userId);

            return new
            {
                UserId = userId,
                TotalPredictions = 42,
                AverageAccuracy = 0.87m,
                GeneratedAt = DateTime.UtcNow,
                ReportType = "UserReport"
            };
        }

        public async Task<object> GenerateSystemReportAsync()
        {
            await Task.Delay(1500);
            _logger.LogInformation("Mock: Generated system report");

            return new
            {
                TotalUsers = 150,
                TotalPredictions = 5847,
                SystemUptime = "99.9%",
                GeneratedAt = DateTime.UtcNow,
                ReportType = "SystemReport"
            };
        }
    }
}