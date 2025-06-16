using CoffeeDiseaseAnalysis.Models.DTOs;

namespace CoffeeDiseaseAnalysis.Services.Interfaces
{
    public interface IMessageQueueService
    {
        Task PublishImageProcessingRequestAsync(ImageProcessingRequest request);
        Task PublishPredictionResultAsync(PredictionResult result);
        Task<bool> IsHealthyAsync();
        void StartConsuming();
        void StopConsuming();
    }
}