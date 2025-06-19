// ==========================================
// File: CoffeeDiseaseAnalysis/Services/ReportService.cs
// ==========================================
using CoffeeDiseaseAnalysis.Services.Interfaces;
using CoffeeDiseaseAnalysis.Data;
using Microsoft.EntityFrameworkCore;

namespace CoffeeDiseaseAnalysis.Services
{
    public class ReportService : IReportService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ReportService> _logger;

        public ReportService(ApplicationDbContext context, ILogger<ReportService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<object> GenerateUserReportAsync(string userId)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    return new { error = "User not found" };
                }

                var totalPredictions = await _context.Predictions
                    .Join(_context.LeafImages, p => p.LeafImageId, l => l.Id, (p, l) => new { p, l })
                    .Where(x => x.l.UserId == userId)
                    .CountAsync();

                var diseaseBreakdown = await _context.Predictions
                    .Join(_context.LeafImages, p => p.LeafImageId, l => l.Id, (p, l) => new { p, l })
                    .Where(x => x.l.UserId == userId)
                    .GroupBy(x => x.p.DiseaseName)
                    .Select(g => new { Disease = g.Key, Count = g.Count() })
                    .ToListAsync();

                var avgConfidence = await _context.Predictions
                    .Join(_context.LeafImages, p => p.LeafImageId, l => l.Id, (p, l) => new { p, l })
                    .Where(x => x.l.UserId == userId)
                    .AverageAsync(x => (double?)x.p.Confidence) ?? 0;

                return new
                {
                    user = new { user.Id, user.UserName, user.Email, user.FullName },
                    statistics = new
                    {
                        totalPredictions,
                        diseaseBreakdown,
                        averageConfidence = Math.Round(avgConfidence, 4)
                    },
                    generatedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating user report");
                return new { error = ex.Message };
            }
        }

        public async Task<object> GenerateSystemReportAsync()
        {
            try
            {
                var totalUsers = await _context.Users.CountAsync();
                var totalPredictions = await _context.Predictions.CountAsync();
                var totalImages = await _context.LeafImages.CountAsync();

                var diseaseStats = await _context.Predictions
                    .GroupBy(p => p.DiseaseName)
                    .Select(g => new { Disease = g.Key, Count = g.Count() })
                    .ToListAsync();

                var modelStats = await _context.ModelVersions
                    .Select(m => new { m.ModelName, m.Version, m.Accuracy, m.IsActive })
                    .ToListAsync();

                var recentActivity = await _context.Predictions
                    .OrderByDescending(p => p.PredictionDate)
                    .Take(10)
                    .Select(p => new { p.DiseaseName, p.Confidence, p.PredictionDate })
                    .ToListAsync();

                return new
                {
                    overview = new { totalUsers, totalPredictions, totalImages },
                    diseaseStatistics = diseaseStats,
                    modelInformation = modelStats,
                    recentActivity,
                    generatedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating system report");
                return new { error = ex.Message };
            }
        }
    }
}