// ===== 4. UPDATE MockMLPService =====
// File: CoffeeDiseaseAnalysis/Services/Mock/MockMLPService.cs
using CoffeeDiseaseAnalysis.Models.DTOs; // ADD THIS USING
using CoffeeDiseaseAnalysis.Services.Interfaces;

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

        // ADD THIS NEW METHOD
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
                ModelVersion = "MLP_v1.0_MOCK"
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

// ===== 5. FIX ENHANCED PREDICTION SERVICE =====
// Update the CombineCnnMlpResults method in EnhancedRealPredictionService
// Replace the existing method with this FIXED version:

/// <summary>
/// Combine CNN and MLP results
/// </summary>
private async Task<decimal> CombineCnnMlpResults(decimal cnnConfidence, List<int> symptomIds)
{
    try
    {
        if (_mlpService != null && symptomIds?.Any() == true)
        {
            var mlpResult = await _mlpService.PredictFromSymptomsDetailedAsync(symptomIds);

            // Weighted combination: CNN 70%, MLP 30%
            var cnnWeight = 0.7m;
            var mlpWeight = 0.3m;

            return (cnnConfidence * cnnWeight) + (mlpResult.Confidence * mlpWeight);
        }

        return cnnConfidence;
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "⚠️ Error combining CNN and MLP results, using CNN only");
        return cnnConfidence;
    }
}