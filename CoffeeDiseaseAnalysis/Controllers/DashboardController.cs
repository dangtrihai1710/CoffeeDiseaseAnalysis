// File: CoffeeDiseaseAnalysis/Controllers/DashboardController.cs - FIXED
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using CoffeeDiseaseAnalysis.Data;
using CoffeeDiseaseAnalysis.Services.Interfaces;

namespace CoffeeDiseaseAnalysis.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin,Expert")]
    public class DashboardController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IPredictionService _predictionService;
        private readonly ICacheService _cacheService;
        private readonly IMessageQueueService _messageQueueService;
        private readonly IWebHostEnvironment _env; // ADDED
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(
            ApplicationDbContext context,
            IPredictionService predictionService,
            ICacheService cacheService,
            IMessageQueueService messageQueueService,
            IWebHostEnvironment env, // ADDED
            ILogger<DashboardController> logger)
        {
            _context = context;
            _predictionService = predictionService;
            _cacheService = cacheService;
            _messageQueueService = messageQueueService;
            _env = env; // ADDED
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
                var endDate = DateTime.UtcNow;
                var startDate = endDate.AddDays(-30); // Last 30 days

                // Basic statistics
                var totalUsers = await _context.Users.CountAsync();
                var totalImages = await _context.LeafImages.CountAsync();
                var totalPredictions = await _context.Predictions.CountAsync();
                var totalFeedbacks = await _context.Feedbacks.CountAsync();

                // Recent statistics (last 30 days)
                var recentPredictions = await _context.Predictions
                    .Where(p => p.PredictionDate >= startDate)
                    .CountAsync();

                var recentImages = await _context.LeafImages
                    .Where(l => l.UploadDate >= startDate)
                    .CountAsync();

                // Model performance
                var currentModel = await _predictionService.GetCurrentModelInfoAsync();

                // Disease distribution
                var diseaseDistribution = await _context.Predictions
                    .Where(p => p.PredictionDate >= startDate)
                    .GroupBy(p => p.DiseaseName)
                    .Select(g => new
                    {
                        Disease = g.Key,
                        Count = g.Count(),
                        AvgConfidence = g.Average(p => (double)p.Confidence),
                        AvgProcessingTime = g.Average(p => (double)p.ProcessingTimeMs)
                    })
                    .OrderByDescending(x => x.Count)
                    .ToListAsync();

                // Processing status
                var processingStatus = await _context.LeafImages
                    .GroupBy(l => l.ImageStatus)
                    .Select(g => new
                    {
                        Status = g.Key,
                        Count = g.Count()
                    })
                    .ToListAsync();

                // Error rate
                var totalLogs = await _context.PredictionLogs
                    .Where(l => l.RequestTime >= startDate)
                    .CountAsync();

                var failedLogs = await _context.PredictionLogs
                    .Where(l => l.RequestTime >= startDate && l.ApiStatus == "Failed")
                    .CountAsync();

                var errorRate = totalLogs > 0 ? (double)failedLogs / totalLogs * 100 : 0;

                // Average processing time
                var avgProcessingTime = await _context.Predictions
                    .Where(p => p.PredictionDate >= startDate)
                    .AverageAsync(p => (double?)p.ProcessingTimeMs) ?? 0;

                return Ok(new
                {
                    SystemStats = new
                    {
                        TotalUsers = totalUsers,
                        TotalImages = totalImages,
                        TotalPredictions = totalPredictions,
                        TotalFeedbacks = totalFeedbacks,
                        RecentPredictions = recentPredictions,
                        RecentImages = recentImages
                    },
                    CurrentModel = new
                    {
                        currentModel.ModelName,
                        currentModel.Version,
                        currentModel.Accuracy,
                        currentModel.TotalPredictions,
                        currentModel.AverageRating
                    },
                    Performance = new
                    {
                        ErrorRate = Math.Round(errorRate, 2),
                        AvgProcessingTime = Math.Round(avgProcessingTime, 2),
                        DiseaseDistribution = diseaseDistribution,
                        ProcessingStatus = processingStatus
                    },
                    Period = new
                    {
                        StartDate = startDate,
                        EndDate = endDate,
                        Days = 30
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting system overview");
                return StatusCode(500, "Có lỗi xảy ra khi lấy tổng quan hệ thống");
            }
        }

        /// <summary>
        /// Thống kê hiệu suất theo thời gian
        /// </summary>
        [HttpGet("performance-metrics")]
        public async Task<ActionResult<object>> GetPerformanceMetrics(
            int days = 7, string groupBy = "day")
        {
            try
            {
                var endDate = DateTime.UtcNow;
                var startDate = endDate.AddDays(-days);

                // Predictions over time
                var predictionsOverTime = await _context.Predictions
                    .Where(p => p.PredictionDate >= startDate)
                    .GroupBy(p => groupBy == "hour"
                        ? new { Year = p.PredictionDate.Year, Month = p.PredictionDate.Month, Day = p.PredictionDate.Day, Hour = p.PredictionDate.Hour }
                        : new { Year = p.PredictionDate.Year, Month = p.PredictionDate.Month, Day = p.PredictionDate.Day, Hour = 0 })
                    .Select(g => new
                    {
                        Date = groupBy == "hour"
                            ? new DateTime(g.Key.Year, g.Key.Month, g.Key.Day, g.Key.Hour, 0, 0)
                            : new DateTime(g.Key.Year, g.Key.Month, g.Key.Day),
                        Count = g.Count(),
                        AvgConfidence = g.Average(p => (double)p.Confidence),
                        AvgProcessingTime = g.Average(p => (double)p.ProcessingTimeMs)
                    })
                    .OrderBy(x => x.Date)
                    .ToListAsync();

                // Error rate over time
                var errorRateOverTime = await _context.PredictionLogs
                    .Where(l => l.RequestTime >= startDate)
                    .GroupBy(l => groupBy == "hour"
                        ? new { Year = l.RequestTime.Year, Month = l.RequestTime.Month, Day = l.RequestTime.Day, Hour = l.RequestTime.Hour }
                        : new { Year = l.RequestTime.Year, Month = l.RequestTime.Month, Day = l.RequestTime.Day, Hour = 0 })
                    .Select(g => new
                    {
                        Date = groupBy == "hour"
                            ? new DateTime(g.Key.Year, g.Key.Month, g.Key.Day, g.Key.Hour, 0, 0)
                            : new DateTime(g.Key.Year, g.Key.Month, g.Key.Day),
                        Total = g.Count(),
                        Failed = g.Count(l => l.ApiStatus == "Failed"),
                        ErrorRate = g.Count() > 0 ? (double)g.Count(l => l.ApiStatus == "Failed") / g.Count() * 100 : 0
                    })
                    .OrderBy(x => x.Date)
                    .ToListAsync();

                // Model accuracy over time (theo feedback)
                var accuracyOverTime = await _context.Feedbacks
                    .Include(f => f.Prediction)
                    .Where(f => f.FeedbackDate >= startDate)
                    .GroupBy(f => groupBy == "hour"
                        ? new { Year = f.FeedbackDate.Year, Month = f.FeedbackDate.Month, Day = f.FeedbackDate.Day, Hour = f.FeedbackDate.Hour }
                        : new { Year = f.FeedbackDate.Year, Month = f.FeedbackDate.Month, Day = f.FeedbackDate.Day, Hour = 0 })
                    .Select(g => new
                    {
                        Date = groupBy == "hour"
                            ? new DateTime(g.Key.Year, g.Key.Month, g.Key.Day, g.Key.Hour, 0, 0)
                            : new DateTime(g.Key.Year, g.Key.Month, g.Key.Day),
                        AvgRating = g.Average(f => (double)f.Rating),
                        TotalFeedbacks = g.Count(),
                        CorrectPredictions = g.Count(f => f.Rating >= 4), // Rating 4-5 = correct
                        AccuracyRate = g.Count() > 0 ? (double)g.Count(f => f.Rating >= 4) / g.Count() * 100 : 0
                    })
                    .OrderBy(x => x.Date)
                    .ToListAsync();

                return Ok(new
                {
                    PredictionsOverTime = predictionsOverTime,
                    ErrorRateOverTime = errorRateOverTime,
                    AccuracyOverTime = accuracyOverTime,
                    Period = new
                    {
                        StartDate = startDate,
                        EndDate = endDate,
                        Days = days,
                        GroupBy = groupBy
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting performance metrics");
                return StatusCode(500, "Có lỗi xảy ra khi lấy thống kê hiệu suất");
            }
        }

        /// <summary>
        /// Thống kê feedback và training data
        /// </summary>
        [HttpGet("feedback-analysis")]
        public async Task<ActionResult<object>> GetFeedbackAnalysis()
        {
            try
            {
                // Feedback distribution
                var feedbackDistribution = await _context.Feedbacks
                    .GroupBy(f => f.Rating)
                    .Select(g => new
                    {
                        Rating = g.Key,
                        Count = g.Count(),
                        Percentage = 0.0 // Will calculate below
                    })
                    .ToListAsync();

                var totalFeedbacks = feedbackDistribution.Sum(f => f.Count);
                feedbackDistribution = feedbackDistribution.Select(f => new
                {
                    f.Rating,
                    f.Count,
                    Percentage = totalFeedbacks > 0 ? Math.Round((double)f.Count / totalFeedbacks * 100, 2) : 0
                }).ToList();

                // Incorrect predictions by disease
                var incorrectPredictions = await _context.Feedbacks
                    .Include(f => f.Prediction)
                    .Where(f => f.Rating <= 2 && !string.IsNullOrEmpty(f.CorrectDiseaseName))
                    .GroupBy(f => new { Original = f.Prediction.DiseaseName, Correct = f.CorrectDiseaseName })
                    .Select(g => new
                    {
                        OriginalPrediction = g.Key.Original,
                        CorrectDisease = g.Key.Correct,
                        Count = g.Count(),
                        AvgConfidence = g.Average(f => (double)f.Prediction.Confidence)
                    })
                    .OrderByDescending(x => x.Count)
                    .Take(10)
                    .ToListAsync();

                // Training data quality
                var trainingDataQuality = await _context.TrainingDataRecords
                    .GroupBy(t => t.Quality)
                    .Select(g => new
                    {
                        Quality = g.Key,
                        Count = g.Count(),
                        Validated = g.Count(t => t.IsValidated),
                        Used = g.Count(t => t.IsUsedForTraining)
                    })
                    .ToListAsync();

                // Recent training data by source
                var trainingDataBySource = await _context.TrainingDataRecords
                    .Where(t => t.CreatedAt >= DateTime.UtcNow.AddDays(-30))
                    .GroupBy(t => t.Source)
                    .Select(g => new
                    {
                        Source = g.Key,
                        Count = g.Count(),
                        Validated = g.Count(t => t.IsValidated)
                    })
                    .ToListAsync();

                // Model improvement suggestions
                var improvementSuggestions = new List<object>();

                // Low confidence predictions
                var lowConfidencePredictions = await _context.Predictions
                    .Where(p => p.Confidence < 0.7m && p.PredictionDate >= DateTime.UtcNow.AddDays(-7))
                    .CountAsync();

                if (lowConfidencePredictions > 10)
                {
                    improvementSuggestions.Add(new
                    {
                        Type = "Low Confidence",
                        Message = $"{lowConfidencePredictions} predictions với confidence < 70% trong 7 ngày qua",
                        Suggestion = "Cần cải thiện model hoặc thu thập thêm training data",
                        Priority = "High"
                    });
                }

                // High error rate for specific disease
                var diseaseErrorRates = await _context.Feedbacks
                    .Include(f => f.Prediction)
                    .Where(f => f.FeedbackDate >= DateTime.UtcNow.AddDays(-7))
                    .GroupBy(f => f.Prediction.DiseaseName)
                    .Select(g => new
                    {
                        Disease = g.Key,
                        ErrorRate = g.Count() > 0 ? (double)g.Count(f => f.Rating <= 2) / g.Count() * 100 : 0,
                        TotalFeedbacks = g.Count()
                    })
                    .Where(x => x.ErrorRate > 30 && x.TotalFeedbacks >= 5)
                    .ToListAsync();

                foreach (var disease in diseaseErrorRates)
                {
                    improvementSuggestions.Add(new
                    {
                        Type = "High Error Rate",
                        Message = $"Bệnh {disease.Disease} có tỷ lệ lỗi {disease.ErrorRate:F1}%",
                        Suggestion = "Cần thu thập thêm training data cho loại bệnh này",
                        Priority = "Medium"
                    });
                }

                return Ok(new
                {
                    FeedbackDistribution = feedbackDistribution,
                    IncorrectPredictions = incorrectPredictions,
                    TrainingDataQuality = trainingDataQuality,
                    TrainingDataBySource = trainingDataBySource,
                    ImprovementSuggestions = improvementSuggestions,
                    Summary = new
                    {
                        TotalFeedbacks = totalFeedbacks,
                        AvgRating = totalFeedbacks > 0
                            ? Math.Round(feedbackDistribution.Sum(f => f.Rating * f.Count) / (double)totalFeedbacks, 2)
                            : 0,
                        TotalTrainingData = trainingDataBySource.Sum(t => t.Count),
                        ValidatedTrainingData = trainingDataBySource.Sum(t => t.Validated)
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting feedback analysis");
                return StatusCode(500, "Có lỗi xảy ra khi phân tích feedback");
            }
        }

        /// <summary>
        /// Health check tổng thể hệ thống
        /// </summary>
        [HttpGet("health-status")]
        public async Task<ActionResult<object>> GetSystemHealthStatus()
        {
            try
            {
                var healthStatus = new
                {
                    Database = await CheckDatabaseHealthAsync(),
                    AIModel = await CheckAIModelHealthAsync(),
                    Cache = await CheckCacheHealthAsync(),
                    MessageQueue = await CheckMessageQueueHealthAsync(),
                    Storage = CheckStorageHealth(),
                    Timestamp = DateTime.UtcNow
                };

                var overallStatus = "Healthy";
                var issues = new List<string>();

                // Use reflection to check IsHealthy property safely
                var databaseHealthy = GetHealthyStatus(healthStatus.Database);
                var aiModelHealthy = GetHealthyStatus(healthStatus.AIModel);
                var cacheHealthy = GetHealthyStatus(healthStatus.Cache);
                var messageQueueHealthy = GetHealthyStatus(healthStatus.MessageQueue);
                var storageHealthy = GetHealthyStatus(healthStatus.Storage);

                if (!databaseHealthy)
                {
                    overallStatus = "Unhealthy";
                    issues.Add("Database connection issues");
                }

                if (!aiModelHealthy)
                {
                    overallStatus = GetStatusValue(healthStatus.AIModel) == "Degraded" ? "Degraded" : "Unhealthy";
                    issues.Add("AI Model issues");
                }

                if (!cacheHealthy)
                {
                    if (overallStatus == "Healthy") overallStatus = "Degraded";
                    issues.Add("Cache issues");
                }

                if (!messageQueueHealthy)
                {
                    if (overallStatus == "Healthy") overallStatus = "Degraded";
                    issues.Add("Message Queue issues");
                }

                return Ok(new
                {
                    OverallStatus = overallStatus,
                    Issues = issues,
                    Components = healthStatus
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting health status");
                return StatusCode(500, "Có lỗi xảy ra khi kiểm tra health status");
            }
        }

        #region Private Health Check Methods

        private bool GetHealthyStatus(object healthObj)
        {
            try
            {
                var type = healthObj.GetType();
                var property = type.GetProperty("IsHealthy");
                if (property?.GetValue(healthObj) is bool isHealthy)
                {
                    return isHealthy;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private string GetStatusValue(object healthObj)
        {
            try
            {
                var type = healthObj.GetType();
                var property = type.GetProperty("Status");
                return property?.GetValue(healthObj)?.ToString() ?? "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }

        private async Task<object> CheckDatabaseHealthAsync()
        {
            try
            {
                await _context.Database.CanConnectAsync();
                var userCount = await _context.Users.CountAsync();

                return new
                {
                    IsHealthy = true,
                    Status = "Healthy",
                    ResponseTime = "< 100ms",
                    LastCheck = DateTime.UtcNow,
                    Details = $"Connection successful, {userCount} users"
                };
            }
            catch (Exception ex)
            {
                return new
                {
                    IsHealthy = false,
                    Status = "Unhealthy",
                    Error = ex.Message,
                    LastCheck = DateTime.UtcNow
                };
            }
        }

        private async Task<object> CheckAIModelHealthAsync()
        {
            try
            {
                var isHealthy = await _predictionService.HealthCheckAsync();
                var modelInfo = await _predictionService.GetCurrentModelInfoAsync();

                return new
                {
                    IsHealthy = isHealthy,
                    Status = isHealthy ? "Healthy" : "Unhealthy",
                    CurrentModel = $"{modelInfo.ModelName} v{modelInfo.Version}",
                    Accuracy = modelInfo.Accuracy,
                    LastCheck = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                return new
                {
                    IsHealthy = false,
                    Status = "Unhealthy",
                    Error = ex.Message,
                    LastCheck = DateTime.UtcNow
                };
            }
        }

        private async Task<object> CheckCacheHealthAsync()
        {
            try
            {
                var isHealthy = await _cacheService.IsHealthyAsync();

                return new
                {
                    IsHealthy = isHealthy,
                    Status = isHealthy ? "Healthy" : "Degraded",
                    Type = "Redis + Memory Cache",
                    LastCheck = DateTime.UtcNow,
                    Details = isHealthy ? "Cache responding normally" : "Using memory cache fallback"
                };
            }
            catch (Exception ex)
            {
                return new
                {
                    IsHealthy = false,
                    Status = "Degraded",
                    Error = ex.Message,
                    LastCheck = DateTime.UtcNow,
                    Details = "Using memory cache only"
                };
            }
        }

        private async Task<object> CheckMessageQueueHealthAsync()
        {
            try
            {
                var isHealthy = await _messageQueueService.IsHealthyAsync();

                return new
                {
                    IsHealthy = isHealthy,
                    Status = isHealthy ? "Healthy" : "Degraded",
                    Type = "RabbitMQ",
                    LastCheck = DateTime.UtcNow,
                    Details = isHealthy ? "Message queue operational" : "Async processing unavailable"
                };
            }
            catch (Exception ex)
            {
                return new
                {
                    IsHealthy = false,
                    Status = "Degraded",
                    Error = ex.Message,
                    LastCheck = DateTime.UtcNow,
                    Details = "Falling back to synchronous processing"
                };
            }
        }

        private object CheckStorageHealth()
        {
            try
            {
                var uploadsPath = Path.Combine(_env.WebRootPath, "uploads");
                var modelsPath = Path.Combine(_env.WebRootPath, "models");

                var uploadsExists = Directory.Exists(uploadsPath);
                var modelsExists = Directory.Exists(modelsPath);

                var uploadsDiskSpace = 0L;
                var modelsDiskSpace = 0L;

                if (uploadsExists)
                {
                    var uploadsInfo = new DirectoryInfo(uploadsPath);
                    uploadsDiskSpace = uploadsInfo.GetFiles("*", SearchOption.AllDirectories)
                        .Sum(file => file.Length);
                }

                if (modelsExists)
                {
                    var modelsInfo = new DirectoryInfo(modelsPath);
                    modelsDiskSpace = modelsInfo.GetFiles("*", SearchOption.AllDirectories)
                        .Sum(file => file.Length);
                }

                return new
                {
                    IsHealthy = uploadsExists && modelsExists,
                    Status = (uploadsExists && modelsExists) ? "Healthy" : "Warning",
                    UploadsPath = uploadsPath,
                    ModelsPath = modelsPath,
                    DiskUsage = new
                    {
                        UploadsSizeMB = Math.Round(uploadsDiskSpace / 1024.0 / 1024.0, 2),
                        ModelsSizeMB = Math.Round(modelsDiskSpace / 1024.0 / 1024.0, 2)
                    },
                    LastCheck = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                return new
                {
                    IsHealthy = false,
                    Status = "Error",
                    Error = ex.Message,
                    LastCheck = DateTime.UtcNow
                };
            }
        }

        #endregion
    }
}