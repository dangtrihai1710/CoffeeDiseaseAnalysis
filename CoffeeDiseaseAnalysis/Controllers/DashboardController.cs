// ===================================================================
// File: CoffeeDiseaseAnalysis/Controllers/DashboardController.cs - FIXED TYPE CONVERSION
// ===================================================================
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using CoffeeDiseaseAnalysis.Data;
using CoffeeDiseaseAnalysis.Data.Entities;

namespace CoffeeDiseaseAnalysis.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    // ✅ FIXED: Bỏ Authorize để test trước, sau đó có thể thêm lại
    [Authorize(Roles = "Admin,Expert")]
    public class DashboardController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(
            ApplicationDbContext context,
            ILogger<DashboardController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Tổng quan thống kê hệ thống
        /// </summary>
        [HttpGet("overview")]
        public async Task<ActionResult<object>> GetSystemOverview()
        {
            try
            {
                _logger.LogInformation("Getting dashboard overview...");

                var endDate = DateTime.UtcNow;
                var startDate = endDate.AddDays(-30); // Last 30 days

                // Basic statistics với error handling
                var totalUsers = 0;
                var totalImages = 0;
                var totalPredictions = 0;
                var totalFeedbacks = 0;

                try
                {
                    totalUsers = await _context.Users.CountAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to count users: {ex.Message}");
                }

                try
                {
                    totalImages = await _context.LeafImages.CountAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to count images: {ex.Message}");
                }

                try
                {
                    totalPredictions = await _context.Predictions.CountAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to count predictions: {ex.Message}");
                }

                try
                {
                    totalFeedbacks = await _context.Feedbacks.CountAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to count feedbacks: {ex.Message}");
                }

                // Recent statistics (last 30 days)
                var recentPredictions = 0;
                var recentImages = 0;

                try
                {
                    recentPredictions = await _context.Predictions
                        .Where(p => p.PredictionDate >= startDate)
                        .CountAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to count recent predictions: {ex.Message}");
                }

                try
                {
                    recentImages = await _context.LeafImages
                        .Where(l => l.UploadDate >= startDate)
                        .CountAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to count recent images: {ex.Message}");
                }

                // Disease distribution
                var diseaseDistribution = new List<object>();
                try
                {
                    diseaseDistribution = await _context.Predictions
                        .GroupBy(p => p.DiseaseName)
                        .Select(g => new
                        {
                            name = g.Key,
                            count = g.Count(),
                            percentage = totalPredictions > 0 ? Math.Round((double)g.Count() / totalPredictions * 100, 1) : 0.0
                        })
                        .OrderByDescending(x => x.count)
                        .Take(10)
                        .ToListAsync<object>();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to get disease distribution: {ex.Message}");
                }

                // ✅ FIXED: Accuracy calculation với proper decimal to double conversion
                var averageConfidence = 0.0;
                try
                {
                    var confidenceValues = await _context.Predictions
                        .Select(p => p.Confidence)
                        .ToListAsync();

                    if (confidenceValues.Any())
                    {
                        averageConfidence = confidenceValues.Average(c => (double)c);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to calculate average confidence: {ex.Message}");
                }

                // ✅ FIXED: Average rating với proper decimal to double conversion
                var averageRating = 0.0;
                try
                {
                    var ratingValues = await _context.Feedbacks
                        .Select(f => f.Rating)
                        .ToListAsync();

                    if (ratingValues.Any())
                    {
                        averageRating = ratingValues.Average(r => (double)r);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to calculate average rating: {ex.Message}");
                }

                var result = new
                {
                    // Basic metrics
                    TotalUsers = totalUsers,
                    TotalImages = totalImages,
                    TotalPredictions = totalPredictions,
                    TotalFeedbacks = totalFeedbacks,

                    // Recent activity
                    RecentPredictions = recentPredictions,
                    RecentImages = recentImages,

                    // Performance metrics
                    Accuracy = Math.Round(averageConfidence, 3),
                    AverageRating = Math.Round(averageRating, 2),

                    // Disease insights
                    DiseaseDistribution = diseaseDistribution,

                    // Meta info
                    LastUpdated = DateTime.UtcNow,
                    DataPeriod = new
                    {
                        StartDate = startDate,
                        EndDate = endDate,
                        Days = 30
                    }
                };

                _logger.LogInformation("Dashboard overview generated successfully");
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating dashboard overview");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi tải dashboard overview",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Thống kê hiệu suất theo thời gian
        /// </summary>
        [HttpGet("performance-metrics")]
        public async Task<ActionResult<object>> GetPerformanceMetrics(
            [FromQuery] int days = 7,
            [FromQuery] string groupBy = "day")
        {
            try
            {
                _logger.LogInformation($"Getting performance metrics for {days} days, grouped by {groupBy}");

                var endDate = DateTime.UtcNow;
                var startDate = endDate.AddDays(-days);

                // Predictions over time
                var predictionsOverTime = new List<object>();

                try
                {
                    if (groupBy.ToLower() == "hour")
                    {
                        var hourlyData = await _context.Predictions
                            .Where(p => p.PredictionDate >= startDate)
                            .GroupBy(p => new
                            {
                                Year = p.PredictionDate.Year,
                                Month = p.PredictionDate.Month,
                                Day = p.PredictionDate.Day,
                                Hour = p.PredictionDate.Hour
                            })
                            .Select(g => new
                            {
                                Year = g.Key.Year,
                                Month = g.Key.Month,
                                Day = g.Key.Day,
                                Hour = g.Key.Hour,
                                Count = g.Count(),
                                ConfidenceValues = g.Select(p => p.Confidence).ToList()
                            })
                            .ToListAsync();

                        predictionsOverTime = hourlyData.Select(g => new
                        {
                            date = new DateTime(g.Year, g.Month, g.Day, g.Hour, 0, 0).ToString("yyyy-MM-dd HH:00"),
                            count = g.Count,
                            avgConfidence = g.ConfidenceValues.Any() ? Math.Round(g.ConfidenceValues.Average(c => (double)c), 3) : 0.0
                        }).OrderBy(x => x.date).ToList<object>();
                    }
                    else // day
                    {
                        var dailyData = await _context.Predictions
                            .Where(p => p.PredictionDate >= startDate)
                            .GroupBy(p => p.PredictionDate.Date)
                            .Select(g => new
                            {
                                Date = g.Key,
                                Count = g.Count(),
                                ConfidenceValues = g.Select(p => p.Confidence).ToList()
                            })
                            .ToListAsync();

                        predictionsOverTime = dailyData.Select(g => new
                        {
                            date = g.Date.ToString("yyyy-MM-dd"),
                            count = g.Count,
                            avgConfidence = g.ConfidenceValues.Any() ? Math.Round(g.ConfidenceValues.Average(c => (double)c), 3) : 0.0
                        }).OrderBy(x => x.date).ToList<object>();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to get predictions over time: {ex.Message}");
                }

                // ✅ FIXED: Model performance by disease với proper decimal handling
                var modelPerformance = new List<object>();
                try
                {
                    var diseaseData = await _context.Predictions
                        .Where(p => p.PredictionDate >= startDate)
                        .GroupBy(p => p.DiseaseName)
                        .Select(g => new
                        {
                            Disease = g.Key,
                            Count = g.Count(),
                            ConfidenceValues = g.Select(p => p.Confidence).ToList()
                        })
                        .ToListAsync();

                    modelPerformance = diseaseData.Select(g => new
                    {
                        disease = g.Disease,
                        count = g.Count,
                        avgConfidence = g.ConfidenceValues.Any() ? Math.Round(g.ConfidenceValues.Average(c => (double)c), 3) : 0.0,
                        minConfidence = g.ConfidenceValues.Any() ? Math.Round(g.ConfidenceValues.Min(c => (double)c), 3) : 0.0,
                        maxConfidence = g.ConfidenceValues.Any() ? Math.Round(g.ConfidenceValues.Max(c => (double)c), 3) : 0.0
                    }).OrderByDescending(x => x.count).ToList<object>();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to get model performance: {ex.Message}");
                }

                // Response time statistics
                var avgResponseTime = 0.0;
                try
                {
                    var responseTimes = await _context.PredictionLogs
                        .Where(log => log.RequestTime >= startDate && log.ResponseTime.HasValue)
                        .Select(log => new
                        {
                            RequestTime = log.RequestTime,
                            ResponseTime = log.ResponseTime.Value
                        })
                        .ToListAsync();

                    if (responseTimes.Any())
                    {
                        avgResponseTime = responseTimes.Average(rt => (rt.ResponseTime - rt.RequestTime).TotalMilliseconds);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to calculate response time: {ex.Message}");
                }

                var result = new
                {
                    PredictionsOverTime = predictionsOverTime,
                    ModelPerformance = modelPerformance,
                    AverageResponseTime = Math.Round(avgResponseTime, 2),
                    TotalPredictions = predictionsOverTime.Cast<dynamic>().Sum(x => (int)x.count),
                    Period = new
                    {
                        StartDate = startDate,
                        EndDate = endDate,
                        Days = days,
                        GroupBy = groupBy
                    }
                };

                _logger.LogInformation("Performance metrics generated successfully");
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating performance metrics");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi tải performance metrics",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Phân tích phản hồi người dùng
        /// </summary>
        [HttpGet("feedback-analysis")]
        public async Task<ActionResult<object>> GetFeedbackAnalysis()
        {
            try
            {
                _logger.LogInformation("Getting feedback analysis...");

                // Feedback distribution
                var feedbackDistribution = new List<object>();
                try
                {
                    feedbackDistribution = await _context.Feedbacks
                        .GroupBy(f => f.Rating)
                        .Select(g => new
                        {
                            name = $"{g.Key} sao",
                            count = g.Count(),
                            rating = g.Key
                        })
                        .OrderBy(x => x.rating)
                        .ToListAsync<object>();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to get feedback distribution: {ex.Message}");
                }

                // Recent feedback comments
                var recentComments = new List<object>();
                try
                {
                    recentComments = await _context.Feedbacks
                        .Where(f => !string.IsNullOrEmpty(f.FeedbackText))
                        .OrderByDescending(f => f.FeedbackDate)
                        .Take(10)
                        .Select(f => new
                        {
                            id = f.Id,
                            text = f.FeedbackText,
                            rating = f.Rating,
                            date = f.FeedbackDate,
                            userId = f.UserId
                        })
                        .ToListAsync<object>();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to get recent comments: {ex.Message}");
                }

                // ✅ FIXED: Feedback by disease với proper decimal handling
                var feedbackByDisease = new List<object>();
                try
                {
                    var diseaseRatings = await _context.Feedbacks
                        .Join(_context.Predictions, f => f.PredictionId, p => p.Id, (f, p) => new { f, p })
                        .GroupBy(x => x.p.DiseaseName)
                        .Select(g => new
                        {
                            Disease = g.Key,
                            Ratings = g.Select(x => x.f.Rating).ToList(),
                            TotalFeedbacks = g.Count()
                        })
                        .ToListAsync();

                    feedbackByDisease = diseaseRatings.Select(g => new
                    {
                        disease = g.Disease,
                        averageRating = g.Ratings.Any() ? Math.Round(g.Ratings.Average(r => (double)r), 2) : 0.0,
                        totalFeedbacks = g.TotalFeedbacks,
                        positivePercentage = g.Ratings.Any() ? Math.Round((double)g.Ratings.Count(r => r >= 4) / g.Ratings.Count * 100, 1) : 0.0
                    }).OrderByDescending(x => x.totalFeedbacks).ToList<object>();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to get feedback by disease: {ex.Message}");
                }

                // ✅ FIXED: Summary calculations
                var totalFeedbacks = feedbackDistribution.Cast<dynamic>().Sum(x => (int)x.count);
                var averageRating = 0.0;
                var positiveFeedbackPercentage = 0.0;

                if (totalFeedbacks > 0)
                {
                    var weightedSum = feedbackDistribution.Cast<dynamic>().Sum(x => (int)x.rating * (int)x.count);
                    averageRating = Math.Round((double)weightedSum / totalFeedbacks, 2);

                    var positiveCount = feedbackDistribution.Cast<dynamic>().Where(x => (int)x.rating >= 4).Sum(x => (int)x.count);
                    positiveFeedbackPercentage = Math.Round((double)positiveCount / totalFeedbacks * 100, 1);
                }

                var result = new
                {
                    FeedbackDistribution = feedbackDistribution,
                    RecentComments = recentComments,
                    FeedbackByDisease = feedbackByDisease,
                    Summary = new
                    {
                        TotalFeedbacks = totalFeedbacks,
                        AverageRating = averageRating,
                        PositiveFeedbackPercentage = positiveFeedbackPercentage
                    }
                };

                _logger.LogInformation("Feedback analysis generated successfully");
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating feedback analysis");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi tải feedback analysis",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Trạng thái sức khỏe hệ thống
        /// </summary>
        [HttpGet("health-status")]
        public async Task<ActionResult<object>> GetHealthStatus()
        {
            try
            {
                _logger.LogInformation("Getting system health status...");

                // Database health
                var databaseStatus = "Healthy";
                var databaseLatency = 0.0;

                try
                {
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    await _context.Database.ExecuteSqlRawAsync("SELECT 1");
                    stopwatch.Stop();
                    databaseLatency = stopwatch.ElapsedMilliseconds;
                }
                catch (Exception ex)
                {
                    databaseStatus = "Unhealthy";
                    _logger.LogWarning($"Database health check failed: {ex.Message}");
                }

                // API status
                var apiStatus = "Healthy";
                var totalRequests = 0;
                var errorRate = 0.0;

                try
                {
                    var recentLogs = await _context.PredictionLogs
                        .Where(log => log.RequestTime >= DateTime.UtcNow.AddHours(-1))
                        .ToListAsync();

                    totalRequests = recentLogs.Count;
                    if (totalRequests > 0)
                    {
                        var errorCount = recentLogs.Count(log => log.ApiStatus != "Success");
                        errorRate = Math.Round((double)errorCount / totalRequests * 100, 2);

                        if (errorRate > 10)
                            apiStatus = "Degraded";
                        if (errorRate > 25)
                            apiStatus = "Unhealthy";
                    }
                }
                catch (Exception ex)
                {
                    apiStatus = "Unknown";
                    _logger.LogWarning($"API status check failed: {ex.Message}");
                }

                // ✅ FIXED: Model status với proper decimal handling
                var modelStatus = "Healthy";
                var modelVersion = "Unknown";
                var modelAccuracy = 0.0;

                try
                {
                    var currentModel = await _context.ModelVersions
                        .OrderByDescending(m => m.CreatedAt)
                        .FirstOrDefaultAsync();

                    if (currentModel != null)
                    {
                        modelVersion = currentModel.Version ?? "Unknown";
                        modelAccuracy = (double)currentModel.Accuracy;

                        if (modelAccuracy < 0.7)
                            modelStatus = "Degraded";
                        if (modelAccuracy < 0.5)
                            modelStatus = "Unhealthy";
                    }
                    else
                    {
                        modelStatus = "Unknown";
                    }
                }
                catch (Exception ex)
                {
                    modelStatus = "Unknown";
                    _logger.LogWarning($"Model status check failed: {ex.Message}");
                }

                // System resources (simplified)
                var memoryUsage = GC.GetTotalMemory(false) / (1024 * 1024); // MB

                var result = new
                {
                    ApiStatus = apiStatus,
                    DatabaseStatus = databaseStatus,
                    ModelStatus = modelStatus,
                    Details = new
                    {
                        Database = new
                        {
                            Status = databaseStatus,
                            LatencyMs = databaseLatency,
                            Connected = databaseStatus == "Healthy"
                        },
                        Api = new
                        {
                            Status = apiStatus,
                            TotalRequestsLastHour = totalRequests,
                            ErrorRatePercentage = errorRate
                        },
                        Model = new
                        {
                            Status = modelStatus,
                            Version = modelVersion,
                            Accuracy = Math.Round(modelAccuracy, 3)
                        },
                        System = new
                        {
                            MemoryUsageMB = memoryUsage,
                            Uptime = Environment.TickCount64 / 1000 / 60, // minutes
                            Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown"
                        }
                    },
                    Timestamp = DateTime.UtcNow,
                    OverallStatus = (apiStatus == "Healthy" && databaseStatus == "Healthy" && modelStatus == "Healthy") ? "Healthy" : "Degraded"
                };

                _logger.LogInformation("System health status generated successfully");
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating health status");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi tải health status",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Test endpoint để kiểm tra controller hoạt động
        /// </summary>
        [HttpGet("test")]
        public IActionResult Test()
        {
            return Ok(new
            {
                success = true,
                message = "Dashboard Controller is working!",
                timestamp = DateTime.UtcNow,
                environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            });
        }
    }
}