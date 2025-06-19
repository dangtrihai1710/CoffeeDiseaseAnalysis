// ==========================================
// File: CoffeeDiseaseAnalysis/Services/Interfaces/IFeedbackService.cs
// ==========================================
namespace CoffeeDiseaseAnalysis.Services.Interfaces
{
    public interface IFeedbackService
    {
        Task<object> SubmitFeedbackAsync(int predictionId, string userId, string feedbackText, int rating);
        Task<object> GetFeedbackAsync(int predictionId);
        Task<object> GetUserFeedbackAsync(string userId);
        Task<bool> IsHealthyAsync();
    }
}