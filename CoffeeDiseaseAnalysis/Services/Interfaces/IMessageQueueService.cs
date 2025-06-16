// File: CoffeeDiseaseAnalysis/Services/Interfaces/IMessageQueueService.cs - FIXED
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

    // Moved ImageProcessingRequest to separate DTOs file or here
    public class ImageProcessingRequest
    {
        public int LeafImageId { get; set; }
        public string ImagePath { get; set; } = string.Empty;
        public List<int>? SymptomIds { get; set; }
        public string UserId { get; set; } = string.Empty;
        public DateTime RequestTime { get; set; }
        public string RequestId { get; set; } = string.Empty;
    }
}