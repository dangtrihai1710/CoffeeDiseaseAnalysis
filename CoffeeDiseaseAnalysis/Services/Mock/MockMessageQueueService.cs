// File: CoffeeDiseaseAnalysis/Services/Mock/MockMessageQueueService.cs
using CoffeeDiseaseAnalysis.Models.DTOs;
using CoffeeDiseaseAnalysis.Services.Interfaces;

namespace CoffeeDiseaseAnalysis.Services.Mock
{
    public class MockMessageQueueService : IMessageQueueService
    {
        private readonly ILogger<MockMessageQueueService> _logger;

        public MockMessageQueueService(ILogger<MockMessageQueueService> logger)
        {
            _logger = logger;
        }

        public async Task PublishImageProcessingRequestAsync(ImageProcessingRequest request)
        {
            await Task.Delay(100);
            _logger.LogInformation("Mock: Published image processing request {RequestId}", request.RequestId);
        }

        public async Task PublishPredictionResultAsync(PredictionResult result)
        {
            await Task.Delay(50);
            _logger.LogInformation("Mock: Published prediction result for {Disease}", result.DiseaseName);
        }

        public async Task<bool> IsHealthyAsync()
        {
            await Task.Delay(25);
            return true;
        }

        public void StartConsuming()
        {
            _logger.LogInformation("Mock: Started consuming messages");
        }

        public void StopConsuming()
        {
            _logger.LogInformation("Mock: Stopped consuming messages");
        }
    }
}