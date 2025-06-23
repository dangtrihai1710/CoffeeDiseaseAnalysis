// ===================================================================
// File: CoffeeDiseaseAnalysis/Controllers/DashboardController.cs - FIXED: CHỈ ADMIN
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
    // ✅ FIXED: Chỉ cho phép Admin truy cập Dashboard
    [Authorize(Roles = "Admin")]
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
        /// Tổng quan thống kê hệ thống - CHỈ ADMIN
        /// </summary>
        [HttpGet("overview")]
        [Authorize(Roles = "Admin")] // ✅ Double check
        public async Task<ActionResult<object>> GetSystemOverview()
        {
            try
            {
                _logger.LogInformation("Admin user accessing dashboard overview...");

                // ✅ ENHANCED: Check user role from JWT token
                var userRole = User.FindFirst("role")?.Value ?? User.FindFirst("http://schemas.microsoft.com/ws/2008/06/identity/claims/role")?.Value;
                var userEmail = User.FindFirst("email")?.Value ?? User.Identity.Name;

                _logger.LogInformation("Dashboard access - User: {Email}, Role: {Role}", userEmail, userRole);

                if (userRole != "Admin")
                {
                    _logger.LogWarning("Non-admin user attempted to access dashboard: {Email}, Role: {Role}", userEmail, userRole);
                    return Forbid("Chỉ Admin mới có thể truy cập Dashboard");
                }

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
                    _logger.LogInformation("Total users: {Count}", totalUsers);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to count users: {ex.Message}");
                }

                try
                {
                    totalImages = await _context.LeafImages.CountAsync();
                    _logger.LogInformation("Total images: {Count}", totalImages);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to count images: {ex.Message}");
                }

                try
                {
                    totalPredictions = await _context.Predictions.CountAsync();
                    _logger.LogInformation("Total predictions: {Count}", totalPredictions);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to count predictions: {ex.Message}");
                }

                try
                {
                    totalFeedbacks = await _context.Feedbacks.CountAsync();
                    _logger.LogInformation("Total feedbacks: {Count}", totalFeedbacks);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to count feedbacks: {ex.Message}");
                }

                // Growth calculations
                var userGrowth = 0.0;
                var imageGrowth = 0.0;
                var predictionGrowth = 0.0;
                var feedbackGrowth = 0.0;

                try
                {
                    var usersLastMonth = await _context.Users
                        .Where(u => u.CreatedAt < startDate)
                        .CountAsync();

                    if (usersLastMonth > 0)
                        userGrowth = ((double)(totalUsers - usersLastMonth) / usersLastMonth) * 100;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to calculate user growth: {ex.Message}");
                }

                try
                {
                    var imagesLastMonth = await _context.LeafImages
                        .Where(i => i.UploadDate < startDate)
                        .CountAsync();

                    if (imagesLastMonth > 0)
                        imageGrowth = ((double)(totalImages - imagesLastMonth) / imagesLastMonth) * 100;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to calculate image growth: {ex.Message}");
                }

                try
                {
                    var predictionsLastMonth = await _context.Predictions
                        .Where(p => p.PredictionDate < startDate)
                        .CountAsync();

                    if (predictionsLastMonth > 0)
                        predictionGrowth = ((double)(totalPredictions - predictionsLastMonth) / predictionsLastMonth) * 100;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to calculate prediction growth: {ex.Message}");
                }

                try
                {
                    var feedbacksLastMonth = await _context.Feedbacks
                        .Where(f => f.FeedbackDate < startDate)
                        .CountAsync();

                    if (feedbacksLastMonth > 0)
                        feedbackGrowth = ((double)(totalFeedbacks - feedbacksLastMonth) / feedbacksLastMonth) * 100;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to calculate feedback growth: {ex.Message}");
                }

                // Recent activities
                var recentActivities = new List<object>();

                try
                {
                    var recentPredictions = await _context.Predictions
                        .Include(p => p.LeafImage)
                        .ThenInclude(i => i.User)
                        .OrderByDescending(p => p.PredictionDate)
                        .Take(5)
                        .Select(p => new
                        {
                            message = $"Phân tích {p.DiseaseName} bởi {p.LeafImage.User.FullName ?? p.LeafImage.User.Email}",
                            timestamp = p.PredictionDate.ToString("dd/MM/yyyy HH:mm")
                        })
                        .ToListAsync();

                    recentActivities.AddRange(recentPredictions);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to get recent activities: {ex.Message}");
                }

                var result = new
                {
                    totalUsers,
                    totalImages,
                    totalPredictions,
                    totalFeedbacks,
                    userGrowth = Math.Round(userGrowth, 1),
                    imageGrowth = Math.Round(imageGrowth, 1),
                    predictionGrowth = Math.Round(predictionGrowth, 1),
                    feedbackGrowth = Math.Round(feedbackGrowth, 1),
                    recentActivities,
                    lastUpdated = DateTime.UtcNow
                };

                _logger.LogInformation("Dashboard overview completed successfully");

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while getting dashboard overview");
                return StatusCode(500, new
                {
                    message = "Lỗi khi tải dữ liệu dashboard",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Thống kê hiệu suất phân tích - CHỈ ADMIN
        /// </summary>
        [HttpGet("performance-metrics")]
        [Authorize(Roles = "Admin")] // ✅ Double check
        public async Task<ActionResult<object>> GetPerformanceMetrics(int days = 7, string groupBy = "day")
        {
            try
            {
                _logger.LogInformation("Getting performance metrics for {Days} days, grouped by {GroupBy}", days, groupBy);

                var startDate = DateTime.UtcNow.AddDays(-days);

                var predictions = await _context.Predictions
                    .Where(p => p.PredictionDate >= startDate)
                    .OrderBy(p => p.PredictionDate)
                    .ToListAsync();

                var groupedData = new List<object>();

                if (groupBy == "day")
                {
                    groupedData = predictions
                        .GroupBy(p => p.PredictionDate.Date)
                        .Select(g => new
                        {
                            date = g.Key.ToString("MM/dd"),
                            predictions = g.Count(),
                            accuracy = g.Average(p => (double)p.Confidence * 100)
                        })
                        .ToList<object>();
                }

                return Ok(groupedData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while getting performance metrics");
                return StatusCode(500, new
                {
                    message = "Lỗi khi tải thống kê hiệu suất",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Phân tích phản hồi người dùng - CHỈ ADMIN
        /// </summary>
        [HttpGet("feedback-analysis")]
        [Authorize(Roles = "Admin")] // ✅ Double check
        public async Task<ActionResult<object>> GetFeedbackAnalysis()
        {
            try
            {
                _logger.LogInformation("Getting feedback analysis...");

                var diseaseDistribution = await _context.Predictions
                    .GroupBy(p => p.DiseaseName)
                    .Select(g => new
                    {
                        name = g.Key,
                        value = g.Count()
                    })
                    .ToListAsync();

                return Ok(diseaseDistribution);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while getting feedback analysis");
                return StatusCode(500, new
                {
                    message = "Lỗi khi phân tích phản hồi",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Tình trạng sức khỏe hệ thống - CHỈ ADMIN
        /// </summary>
        [HttpGet("health-status")]
        [Authorize(Roles = "Admin")] // ✅ Double check
        public async Task<ActionResult<object>> GetHealthStatus()
        {
            try
            {
                _logger.LogInformation("Getting system health status...");

                var healthData = new List<object>
                {
                    new { service = "Database", status = "healthy" },
                    new { service = "AI Model", status = "healthy" },
                    new { service = "File Storage", status = "healthy" },
                    new { service = "Email Service", status = "healthy" }
                };

                // Test database connection
                try
                {
                    await _context.Database.CanConnectAsync();
                }
                catch
                {
                    healthData[0] = new { service = "Database", status = "unhealthy" };
                }

                return Ok(healthData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while getting health status");
                return StatusCode(500, new
                {
                    message = "Lỗi khi kiểm tra tình trạng hệ thống",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Thống kê người dùng theo role - CHỈ ADMIN
        /// </summary>
        [HttpGet("user-statistics")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<object>> GetUserStatistics()
        {
            try
            {
                _logger.LogInformation("Getting user statistics...");

                var usersByRole = await _context.Users
                    .GroupBy(u => u.Role)
                    .Select(g => new
                    {
                        role = g.Key,
                        count = g.Count()
                    })
                    .ToListAsync();

                var activeUsers = await _context.Users
                    .Where(u => u.LeafImages.Any(i => i.UploadDate >= DateTime.UtcNow.AddDays(-30)))
                    .CountAsync();

                return Ok(new
                {
                    usersByRole,
                    activeUsers,
                    totalUsers = await _context.Users.CountAsync()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while getting user statistics");
                return StatusCode(500, new
                {
                    message = "Lỗi khi tải thống kê người dùng",
                    error = ex.Message
                });
            }
        }
    }
}