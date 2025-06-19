// ==========================================
// File: CoffeeDiseaseAnalysis/Services/Interfaces/IImageProcessingService.cs
// ==========================================
namespace CoffeeDiseaseAnalysis.Services.Interfaces
{
    public interface IImageProcessingService
    {
        Task<byte[]> PreprocessImageAsync(byte[] imageBytes);
        Task<string> CalculateImageHashAsync(byte[] imageBytes);
        Task<bool> ValidateImageAsync(byte[] imageBytes);
        Task<bool> IsHealthyAsync();
    }
}