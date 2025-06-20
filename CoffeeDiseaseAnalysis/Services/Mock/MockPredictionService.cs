// File: CoffeeDiseaseAnalysis/Services/Mock/MockPredictionService.cs
using CoffeeDiseaseAnalysis.Models.DTOs;
using CoffeeDiseaseAnalysis.Services.Interfaces;

namespace CoffeeDiseaseAnalysis.Services.Mock
{
    public class MockPredictionService : IPredictionService
    {
        private readonly ILogger<MockPredictionService> _logger;
        private readonly string[] _diseaseClasses = { "Cercospora", "Healthy", "Miner", "Phoma", "Rust" };

        public MockPredictionService(ILogger<MockPredictionService> logger)
        {
            _logger = logger;
        }

        public async Task<PredictionResult> PredictDiseaseAsync(byte[] imageBytes, string imagePath, List<int>? symptomIds = null)
        {
            await Task.Delay(500);
            var random = new Random();
            var selectedDisease = _diseaseClasses[random.Next(_diseaseClasses.Length)];
            var confidence = (decimal)(0.6 + random.NextDouble() * 0.35);

            return new PredictionResult
            {
                DiseaseName = selectedDisease,
                Confidence = confidence,
                SeverityLevel = confidence >= 0.85m ? "Cao" : confidence >= 0.70m ? "Trung Bình" : "Thấp",
                Description = $"Mock prediction for {selectedDisease}",
                ModelVersion = "MockModel_v1.0",
                PredictionDate = DateTime.UtcNow,
                ProcessingTimeMs = random.Next(300, 800),
                TreatmentSuggestion = "Mock treatment suggestion",
                ImagePath = imagePath
            };
        }

        public async Task<BatchPredictionResponse> PredictBatchAsync(List<byte[]> imagesBytes, List<string> imagePaths)
        {
            var response = new BatchPredictionResponse();
            for (int i = 0; i < imagesBytes.Count; i++)
            {
                var result = await PredictDiseaseAsync(imagesBytes[i], imagePaths[i]);
                response.Results.Add(result);
                response.SuccessCount++;
            }
            response.TotalProcessed = imagesBytes.Count;
            return response;
        }

        public async Task<ModelStatistics> GetCurrentModelInfoAsync()
        {
            await Task.Delay(100);
            return new ModelStatistics
            {
                ModelVersion = "MockModel_v1.0",
                AccuracyRate = 0.87m,
                TotalPredictions = 1250,
                AverageConfidence = 0.82m,
                AverageProcessingTime = 650,
                LastTrainingDate = DateTime.UtcNow.AddDays(-15),
                ModelSize = "25.6 MB",
                IsActive = true
            };
        }

        public async Task<bool> SwitchModelVersionAsync(string modelVersion)
        {
            await Task.Delay(100);
            return true;
        }

        public async Task<bool> HealthCheckAsync()
        {
            await Task.Delay(50);
            return true;
        }
    }
}