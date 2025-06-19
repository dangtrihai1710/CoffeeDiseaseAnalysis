// ==========================================
// File: CoffeeDiseaseAnalysis/Services/DashboardService.cs
// ==========================================
using CoffeeDiseaseAnalysis.Services.Interfaces;
using CoffeeDiseaseAnalysis.Data;
using Microsoft.EntityFrameworkCore;

namespace CoffeeDiseaseAnalysis.Services
{
    public class DashboardService : IDashboardService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DashboardService> _logger;

        public DashboardService(ApplicationDbContext context, ILogger<DashboardService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<object> GetOverviewAsync()
        {
            try
            {
                var totalPredictions = await _context.Predictions.CountAsync();
                var totalUsers = await _context.Users.CountAsync();
                var totalImages = await _context.LeafImages.CountAsync();
                var todayPredictions = await _context.Predictions
                    .Where(p => p.PredictionDate.Date == DateTime.Today)
                    .CountAsync();

                return new
                {
                    totalPredictions,
                    totalUsers,
                    totalImages,
                    todayPredictions,
                    timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting dashboard overview");
                return new { error = "Failed to get overview", message = ex.Message };
            }
        }

        public async Task<object> GetStatsAsync()
        {
            try
            {
                // Disease distribution
                var diseaseStats = await _context.Predictions
                    .GroupBy(p => p.DiseaseName)
                    .Select(g => new { Disease = g.Key, Count = g.Count() })
                    .ToListAsync();

                // Monthly predictions
                var monthlyStats = await _context.Predictions
                    .Where(p => p.PredictionDate >= DateTime.Now.AddMonths(-6))
                    .GroupBy(p => new { p.PredictionDate.Year, p.PredictionDate.Month })
                    .Select(g => new {
                        Year = g.Key.Year,
                        Month = g.Key.Month,
                        Count = g.Count()
                    })
                    .ToListAsync();

                return new
                {
                    diseaseDistribution = diseaseStats,
                    monthlyTrends = monthlyStats,
                    timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting dashboard stats");
                return new { error = "Failed to get stats", message = ex.Message };
            }
        }

        public async Task<object> GetPerformanceMetricsAsync()
        {
            try
            {
                var avgConfidence = await _context.Predictions
                    .Where(p => p.PredictionDate >= DateTime.Now.AddDays(-30))
                    .AverageAsync(p => (double?)p.Confidence) ?? 0;

                var highConfidencePredictions = await _context.Predictions
                    .Where(p => p.Confidence >= 0.8m)
                    .CountAsync();

                var totalPredictions = await _context.Predictions.CountAsync();
                var accuracyRate = totalPredictions > 0 ? (double)highConfidencePredictions / totalPredictions : 0;

                return new
                {
                    averageConfidence = Math.Round(avgConfidence, 4),
                    accuracyRate = Math.Round(accuracyRate, 4),
                    highConfidencePredictions,
                    totalPredictions,
                    timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting performance metrics");
                return new { error = "Failed to get performance metrics", message = ex.Message };
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