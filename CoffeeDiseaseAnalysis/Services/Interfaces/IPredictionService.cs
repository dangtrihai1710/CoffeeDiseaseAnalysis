// File: CoffeeDiseaseAnalysis/Services/Interfaces/IPredictionService.cs
using CoffeeDiseaseAnalysis.Models.DTOs;

namespace CoffeeDiseaseAnalysis.Services.Interfaces
{
    public interface IPredictionService
    {
        /// <summary>
        /// Dự đoán bệnh từ ảnh lá cà phê
        /// </summary>
        Task<PredictionResult> PredictDiseaseAsync(byte[] imageBytes, string imagePath, List<int>? symptomIds = null);

        /// <summary>
        /// Dự đoán batch nhiều ảnh
        /// </summary>
        Task<BatchPredictionResponse> PredictBatchAsync(List<byte[]> imagesBytes, List<string> imagePaths);

        /// <summary>
        /// Lấy thông tin model hiện tại
        /// </summary>
        Task<ModelStatistics> GetCurrentModelInfoAsync();

        /// <summary>
        /// Chuyển đổi model active
        /// </summary>
        Task<bool> SwitchModelVersionAsync(string modelVersion);

        /// <summary>
        /// Kiểm tra health của AI service
        /// </summary>
        Task<bool> HealthCheckAsync();
    }
}