// File: CoffeeDiseaseAnalysis/Services/Mock/MockModelManagementService.cs
using CoffeeDiseaseAnalysis.Services.Interfaces;

namespace CoffeeDiseaseAnalysis.Services.Mock
{
    public class MockModelManagementService : IModelManagementService
    {
        private readonly ILogger<MockModelManagementService> _logger;

        public MockModelManagementService(ILogger<MockModelManagementService> logger)
        {
            _logger = logger;
        }

        public async Task<object> GetActiveModelAsync()
        {
            await Task.Delay(100);
            return new
            {
                Id = 1,
                Name = "MockModel_v1.0",
                Version = "1.0",
                Accuracy = 0.87m,
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-30)
            };
        }

        public async Task<bool> SwitchModelAsync(int modelId)
        {
            await Task.Delay(200);
            _logger.LogInformation("Mock: Switched to model {ModelId}", modelId);
            return true;
        }

        public async Task<object> GetModelPerformanceAsync(int modelId)
        {
            await Task.Delay(100);
            return new
            {
                ModelId = modelId,
                Accuracy = 0.87m,
                Precision = 0.85m,
                Recall = 0.89m,
                F1Score = 0.87m,
                TotalPredictions = 1250,
                LastEvaluated = DateTime.UtcNow.AddDays(-1)
            };
        }

        public async Task<bool> ValidateModelAsync(string modelPath)
        {
            await Task.Delay(500);
            _logger.LogInformation("Mock: Validated model at {Path}", modelPath);
            return true;
        }

        public async Task<bool> IsHealthyAsync()
        {
            await Task.Delay(50);
            return true;
        }
    }
}
