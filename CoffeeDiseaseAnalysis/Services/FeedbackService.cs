// ==========================================
// File: CoffeeDiseaseAnalysis/Services/FeedbackService.cs
// ==========================================
using CoffeeDiseaseAnalysis.Services.Interfaces;
using CoffeeDiseaseAnalysis.Data;
using CoffeeDiseaseAnalysis.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoffeeDiseaseAnalysis.Services
{
    public class FeedbackService : IFeedbackService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<FeedbackService> _logger;

        public FeedbackService(ApplicationDbContext context, ILogger<FeedbackService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<object> SubmitFeedbackAsync(int predictionId, string userId, string feedbackText, int rating)
        {
            try
            {
                var feedback = new Feedback
                {
                    PredictionId = predictionId,
                    UserId = userId,
                    FeedbackText = feedbackText,
                    Rating = rating,
                    FeedbackDate = DateTime.UtcNow
                };

                _context.Feedbacks.Add(feedback);
                await _context.SaveChangesAsync();

                return new { success = true, feedbackId = feedback.Id };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting feedback");
                return new { success = false, error = ex.Message };
            }
        }

        public async Task<object> GetFeedbackAsync(int predictionId)
        {
            try
            {
                var feedback = await _context.Feedbacks
                    .Where(f => f.PredictionId == predictionId)
                    .Select(f => new
                    {
                        f.Id,
                        f.FeedbackText,
                        f.Rating,
                        f.FeedbackDate,
                        f.UserId
                    })
                    .ToListAsync();

                return feedback;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting feedback");
                return new { error = ex.Message };
            }
        }

        public async Task<object> GetUserFeedbackAsync(string userId)
        {
            try
            {
                var feedback = await _context.Feedbacks
                    .Where(f => f.UserId == userId)
                    .Include(f => f.Prediction)
                    .Select(f => new
                    {
                        f.Id,
                        f.FeedbackText,
                        f.Rating,
                        f.FeedbackDate,
                        Prediction = new
                        {
                            f.Prediction.Id,
                            f.Prediction.DiseaseName,
                            f.Prediction.Confidence
                        }
                    })
                    .ToListAsync();

                return feedback;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user feedback");
                return new { error = ex.Message };
            }
        }

        public async Task<bool> IsHealthyAsync()
        {
            try
            {
                await _context.Database.CanConnectAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}