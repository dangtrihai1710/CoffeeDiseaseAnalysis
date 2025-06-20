// File: CoffeeDiseaseAnalysis/Services/Mock/MockMLPService.cs
using CoffeeDiseaseAnalysis.Services.Interfaces;
using CoffeeDiseaseAnalysis.Models.DTOs;

namespace CoffeeDiseaseAnalysis.Services.Mock
{
    public class MockMLPService : IMLPService
    {
        private readonly ILogger<MockMLPService> _logger;

        public MockMLPService(ILogger<MockMLPService> logger)
        {
            _logger = logger;
        }

        public async Task<decimal> PredictFromSymptomsAsync(List<int> symptomIds)
        {
            await Task.Delay(200);
            var random = new Random();
            return (decimal)(0.5 + random.NextDouble() * 0.4);
        }

        public async Task<MLPPredictionResult> PredictFromSymptomsDetailedAsync(List<int> symptomIds)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            await Task.Delay(250);

            var random = new Random();
            var classes = new[] { "Cercospora", "Healthy", "Miner", "Phoma", "Rust" };
            var probabilities = new Dictionary<string, decimal>();

            foreach (var className in classes)
            {
                probabilities[className] = (decimal)(random.NextDouble());
            }

            var topPrediction = probabilities.OrderByDescending(x => x.Value).First();

            return new MLPPredictionResult
            {
                DiseaseName = topPrediction.Key,
                Confidence = topPrediction.Value,
                AllClassProbabilities = probabilities,
                PredictionDate = DateTime.UtcNow,
                ProcessingTimeMs = (int)stopwatch.ElapsedMilliseconds,
                ModelVersion = "MLP_v1.0_MOCK",
                TotalSymptoms = symptomIds?.Count ?? 0,
                Features = symptomIds?.Select(id => $"Symptom_{id}").ToList() ?? new(),
                IsReliable = (symptomIds?.Count ?? 0) >= 3
            };
        }

        public async Task<Dictionary<string, decimal>> PredictAllClassesFromSymptomsAsync(List<int> symptomIds)
        {
            await Task.Delay(300);
            var random = new Random();
            var classes = new[] { "Cercospora", "Healthy", "Miner", "Phoma", "Rust" };
            var results = new Dictionary<string, decimal>();

            foreach (var className in classes)
            {
                results[className] = (decimal)(random.NextDouble());
            }
            return results;
        }

        public async Task TrainMLPModelAsync()
        {
            await Task.Delay(2000);
            _logger.LogInformation("Mock MLP model training completed");
        }

        public async Task<bool> IsModelAvailableAsync()
        {
            await Task.Delay(50);
            return true;
        }
    }
}