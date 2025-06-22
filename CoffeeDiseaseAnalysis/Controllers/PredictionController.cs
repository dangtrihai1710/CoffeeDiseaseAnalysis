// File: CoffeeDiseaseAnalysis/Controllers/PredictionController.cs - SIMPLIFIED
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using CoffeeDiseaseAnalysis.Data;
using CoffeeDiseaseAnalysis.Data.Entities;
using CoffeeDiseaseAnalysis.Models.DTOs;
using CoffeeDiseaseAnalysis.Services.Interfaces;
using SixLabors.ImageSharp;

namespace CoffeeDiseaseAnalysis.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PredictionController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IPredictionService _predictionService;
        private readonly ILogger<PredictionController> _logger;
        private readonly IWebHostEnvironment _env;

        public PredictionController(
            ApplicationDbContext context,
            UserManager<User> userManager,
            IPredictionService predictionService,
            ILogger<PredictionController> logger,
            IWebHostEnvironment env)
        {
            _context = context;
            _userManager = userManager;
            _predictionService = predictionService;
            _logger = logger;
            _env = env;
        }

        /// <summary>
        /// Upload ảnh và phân tích bệnh lá cà phê - SIMPLIFIED VERSION
        /// Quy trình: Upload → Gọi AI Model → Lưu kết quả → Trả về
        /// </summary>
        [HttpPost("analyze")]
        public async Task<ActionResult<PredictionResult>> AnalyzeLeafImage([FromForm] UploadImageRequest request)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                _logger.LogInformation("🔄 Starting leaf image analysis...");

                // 1. Validate request
                if (request.Image == null || request.Image.Length == 0)
                {
                    return BadRequest("Không có ảnh nào được upload");
                }

                var validationResult = ValidateImageFile(request.Image);
                if (!string.IsNullOrEmpty(validationResult))
                {
                    return BadRequest(validationResult);
                }

                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return Unauthorized();
                }

                _logger.LogInformation("✅ User authenticated: {UserId}", user.Id);

                // 2. Lưu ảnh vào thư mục và database
                var leafImage = await SaveImageFileAsync(request.Image, user.Id);
                _logger.LogInformation("✅ Image saved: {ImageId}, Path: {Path}", leafImage.Id, leafImage.FilePath);

                // 3. Thêm triệu chứng nếu có
                if (request.SymptomIds?.Any() == true)
                {
                    await AddSymptomsToImageAsync(leafImage.Id, request.SymptomIds, request.Notes);
                    _logger.LogInformation("✅ Symptoms added: {Count} symptoms", request.SymptomIds.Count);
                }

                // 4. Gọi AI model để phân tích
                var imageBytes = await GetImageBytesAsync(request.Image);
                var predictionResult = await _predictionService.PredictDiseaseAsync(
                    imageBytes, leafImage.FilePath, request.SymptomIds);

                _logger.LogInformation("✅ AI prediction completed: {Disease} ({Confidence:P})",
                    predictionResult.DiseaseName, predictionResult.Confidence);

                // 5. Lưu kết quả prediction vào database
                var prediction = new Prediction
                {
                    LeafImageId = leafImage.Id,
                    DiseaseName = predictionResult.DiseaseName,
                    Confidence = predictionResult.Confidence,
                    SeverityLevel = predictionResult.SeverityLevel,
                    TreatmentSuggestion = predictionResult.TreatmentSuggestion,
                    PredictionDate = DateTime.UtcNow
                };

                _context.Predictions.Add(prediction);
                await _context.SaveChangesAsync();

                // 6. Cập nhật trạng thái ảnh
                leafImage.ImageStatus = "Processed";
                await _context.SaveChangesAsync();

                // 7. Tạo prediction log để theo dõi hiệu suất
                await CreatePredictionLogAsync(leafImage.Id, startTime, "Success");

                // 8. Chuẩn bị response
                predictionResult.PredictionId = prediction.Id;
                predictionResult.LeafImageId = leafImage.Id;

                _logger.LogInformation("✅ Analysis completed successfully in {ProcessingTime}ms",
                    (DateTime.UtcNow - startTime).TotalMilliseconds);

                return Ok(predictionResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error during leaf image analysis");

                // Tạo error log
                if (ex.Data.Contains("LeafImageId"))
                {
                    await CreatePredictionLogAsync((int)ex.Data["LeafImageId"]!, startTime, "Failed", ex.Message);
                }

                return StatusCode(500, new
                {
                    Message = "Có lỗi xảy ra khi phân tích ảnh. Vui lòng thử lại.",
                    Error = _env.IsDevelopment() ? ex.Message : null
                });
            }
        }

        /// <summary>
        /// Lấy lịch sử phân tích của người dùng
        /// </summary>
        [HttpGet("history")]
        public async Task<ActionResult<object>> GetAnalysisHistory(
            int pageNumber = 1,
            int pageSize = 10,
            string? diseaseFilter = null,
            string? statusFilter = null,
            DateTime? fromDate = null,
            DateTime? toDate = null)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return Unauthorized();
                }

                var query = _context.Predictions
                    .Include(p => p.LeafImage)
                    .Include(p => p.Feedbacks)
                    .Where(p => p.LeafImage.UserId == user.Id);

                // Apply filters
                if (!string.IsNullOrEmpty(diseaseFilter))
                {
                    query = query.Where(p => p.DiseaseName.Contains(diseaseFilter));
                }

                if (!string.IsNullOrEmpty(statusFilter))
                {
                    query = query.Where(p => p.LeafImage.ImageStatus == statusFilter);
                }

                if (fromDate.HasValue)
                {
                    query = query.Where(p => p.PredictionDate >= fromDate.Value);
                }

                if (toDate.HasValue)
                {
                    query = query.Where(p => p.PredictionDate <= toDate.Value);
                }

                var totalCount = await query.CountAsync();

                var predictions = await query
                    .OrderByDescending(p => p.PredictionDate)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(p => new PredictionHistory
                    {
                        Id = p.Id,
                        ImagePath = p.LeafImage.FilePath,
                        DiseaseName = p.DiseaseName,
                        Confidence = p.Confidence,
                        PredictionDate = p.PredictionDate,
                        SeverityLevel = p.SeverityLevel,
                        FeedbackRating = p.Feedbacks.FirstOrDefault() != null ? p.Feedbacks.First().Rating : null,
                        FeedbackText = p.Feedbacks.FirstOrDefault() != null ? p.Feedbacks.First().FeedbackText : null
                    })
                    .ToListAsync();

                return Ok(new
                {
                    Data = predictions,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                    Filters = new
                    {
                        DiseaseFilter = diseaseFilter,
                        StatusFilter = statusFilter,
                        FromDate = fromDate,
                        ToDate = toDate
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting prediction history");
                return StatusCode(500, "Có lỗi xảy ra khi lấy lịch sử phân tích");
            }
        }

        /// <summary>
        /// Lấy chi tiết một kết quả phân tích
        /// </summary>
        [HttpGet("details/{predictionId}")]
        public async Task<ActionResult<object>> GetPredictionDetails(int predictionId)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return Unauthorized();
                }

                var prediction = await _context.Predictions
                    .Include(p => p.LeafImage)
                        .ThenInclude(li => li.LeafImageSymptoms)
                            .ThenInclude(lis => lis.Symptom)
                    .Include(p => p.Feedbacks)
                    .FirstOrDefaultAsync(p => p.Id == predictionId && p.LeafImage.UserId == user.Id);

                if (prediction == null)
                {
                    return NotFound("Không tìm thấy kết quả phân tích");
                }

                var result = new
                {
                    prediction.Id,
                    prediction.DiseaseName,
                    prediction.Confidence,
                    prediction.SeverityLevel,
                    prediction.TreatmentSuggestion,
                    prediction.PredictionDate,
                    ImageInfo = new
                    {
                        prediction.LeafImage.FilePath,
                        prediction.LeafImage.ImageStatus,
                        prediction.LeafImage.UploadDate,
                        prediction.LeafImage.FileSize,
                        prediction.LeafImage.OriginalFileName
                    },
                    Symptoms = prediction.LeafImage.LeafImageSymptoms.Select(lis => new
                    {
                        lis.Symptom.Id,
                        lis.Symptom.Name,
                        lis.Symptom.Description,
                        lis.Symptom.Category,
                        lis.ObservedDate,
                        lis.Notes
                    }).ToList(),
                    Feedbacks = prediction.Feedbacks.Select(f => new
                    {
                        f.Id,
                        f.Rating,
                        f.FeedbackText,
                        f.FeedbackDate
                    }).ToList()
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting prediction details");
                return StatusCode(500, "Có lỗi xảy ra khi lấy chi tiết phân tích");
            }
        }

        /// <summary>
        /// Gửi feedback cho kết quả phân tích
        /// </summary>
        [HttpPost("feedback")]
        public async Task<ActionResult<object>> SubmitFeedback([FromBody] FeedbackRequest request)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return Unauthorized();
                }

                var prediction = await _context.Predictions
                    .Include(p => p.LeafImage)
                    .FirstOrDefaultAsync(p => p.Id == request.PredictionId && p.LeafImage.UserId == user.Id);

                if (prediction == null)
                {
                    return NotFound("Không tìm thấy kết quả phân tích");
                }

                // Kiểm tra xem đã có feedback chưa
                var existingFeedback = await _context.Feedbacks
                    .FirstOrDefaultAsync(f => f.PredictionId == request.PredictionId && f.UserId == user.Id);

                if (existingFeedback != null)
                {
                    // Cập nhật feedback hiện có
                    existingFeedback.Rating = request.Rating;
                    existingFeedback.FeedbackText = request.FeedbackText;
                    existingFeedback.FeedbackDate = DateTime.UtcNow;

                    _logger.LogInformation("✅ Feedback updated: Rating {Rating} for Prediction {PredictionId}",
                        request.Rating, request.PredictionId);
                }
                else
                {
                    // Tạo feedback mới
                    var feedback = new Feedback
                    {
                        PredictionId = request.PredictionId,
                        UserId = user.Id,
                        Rating = request.Rating,
                        FeedbackText = request.FeedbackText,
                        FeedbackDate = DateTime.UtcNow
                    };

                    _context.Feedbacks.Add(feedback);

                    _logger.LogInformation("✅ Feedback submitted: Rating {Rating} for Prediction {PredictionId}",
                        request.Rating, request.PredictionId);
                }

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    Message = "Cảm ơn bạn đã gửi phản hồi!",
                    PredictionId = request.PredictionId,
                    Rating = request.Rating
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting feedback");
                return StatusCode(500, "Có lỗi xảy ra khi gửi phản hồi");
            }
        }

        /// <summary>
        /// Lấy danh sách triệu chứng
        /// </summary>
        [HttpGet("symptoms")]
        public async Task<ActionResult<List<SymptomInfo>>> GetSymptoms()
        {
            try
            {
                var symptoms = await _context.Symptoms
                    .OrderBy(s => s.Category)
                    .ThenBy(s => s.Name)
                    .Select(s => new SymptomInfo
                    {
                        Id = s.Id,
                        Name = s.Name,
                        Description = s.Description,
                        Category = s.Category
                    })
                    .ToListAsync();

                return Ok(symptoms);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting symptoms");
                return StatusCode(500, "Có lỗi xảy ra khi lấy danh sách triệu chứng");
            }
        }

        /// <summary>
        /// Health check endpoint
        /// </summary>
        [HttpGet("health")]
        [AllowAnonymous]
        public async Task<ActionResult<object>> HealthCheck()
        {
            try
            {
                var dbHealthy = await _context.Database.CanConnectAsync();
                var modelAvailable = await _predictionService.IsModelAvailableAsync();

                return Ok(new
                {
                    Status = dbHealthy && modelAvailable ? "Healthy" : "Degraded",
                    Timestamp = DateTime.UtcNow,
                    Services = new
                    {
                        Database = dbHealthy ? "Connected" : "Disconnected",
                        AIModel = modelAvailable ? "Available" : "Unavailable"
                    },
                    Version = "2.0.0-Simplified",
                    Environment = _env.EnvironmentName
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check failed");
                return StatusCode(500, new
                {
                    Status = "Unhealthy",
                    Error = ex.Message,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// Lấy thống kê sử dụng (chỉ dành cho Admin/Expert)
        /// </summary>
        [HttpGet("stats")]
        [Authorize(Roles = "Admin,Expert")]
        public async Task<ActionResult<object>> GetUsageStats(DateTime? fromDate = null, DateTime? toDate = null)
        {
            try
            {
                fromDate ??= DateTime.UtcNow.AddDays(-30);
                toDate ??= DateTime.UtcNow;

                var modelStats = await _predictionService.GetModelStatsAsync();

                var dbStats = new
                {
                    Period = new { From = fromDate, To = toDate },
                    TotalPredictions = await _context.Predictions
                        .Where(p => p.PredictionDate >= fromDate && p.PredictionDate <= toDate)
                        .CountAsync(),
                    TotalImages = await _context.LeafImages
                        .Where(li => li.UploadDate >= fromDate && li.UploadDate <= toDate)
                        .CountAsync(),
                    TotalUsers = await _context.LeafImages
                        .Where(li => li.UploadDate >= fromDate && li.UploadDate <= toDate)
                        .Select(li => li.UserId)
                        .Distinct()
                        .CountAsync(),
                    DiseaseDistribution = await _context.Predictions
                        .Where(p => p.PredictionDate >= fromDate && p.PredictionDate <= toDate)
                        .GroupBy(p => p.DiseaseName)
                        .Select(g => new { Disease = g.Key, Count = g.Count(), Percentage = g.Count() * 100.0 / _context.Predictions.Count() })
                        .OrderByDescending(x => x.Count)
                        .ToListAsync(),
                    AvgConfidence = await _context.Predictions
                        .Where(p => p.PredictionDate >= fromDate && p.PredictionDate <= toDate)
                        .AverageAsync(p => (double)p.Confidence),
                    FeedbackStats = new
                    {
                        TotalFeedbacks = await _context.Feedbacks
                            .Where(f => f.FeedbackDate >= fromDate && f.FeedbackDate <= toDate)
                            .CountAsync(),
                        AvgRating = await _context.Feedbacks
                            .Where(f => f.FeedbackDate >= fromDate && f.FeedbackDate <= toDate)
                            .AverageAsync(f => (double?)f.Rating) ?? 0,
                        RatingDistribution = await _context.Feedbacks
                            .Where(f => f.FeedbackDate >= fromDate && f.FeedbackDate <= toDate)
                            .GroupBy(f => f.Rating)
                            .Select(g => new { Rating = g.Key, Count = g.Count() })
                            .OrderBy(x => x.Rating)
                            .ToListAsync()
                    },
                    PerformanceStats = new
                    {
                        AvgProcessingTime = await _context.PredictionLogs
                            .Where(pl => pl.RequestTime >= fromDate && pl.RequestTime <= toDate && pl.ProcessingTimeMs.HasValue)
                            .AverageAsync(pl => (double?)pl.ProcessingTimeMs) ?? 0,
                        SuccessRate = await _context.PredictionLogs
                            .Where(pl => pl.RequestTime >= fromDate && pl.RequestTime <= toDate)
                            .GroupBy(pl => pl.ApiStatus)
                            .Select(g => new { Status = g.Key, Count = g.Count() })
                            .ToListAsync()
                    }
                };

                return Ok(new
                {
                    ModelInfo = modelStats,
                    UsageStats = dbStats,
                    GeneratedAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting usage stats");
                return StatusCode(500, "Có lỗi xảy ra khi lấy thống kê sử dụng");
            }
        }

        #region Private Helper Methods

        private string ValidateImageFile(IFormFile file)
        {
            // Kiểm tra kích thước file (max 10MB)
            if (file.Length > 10 * 1024 * 1024)
            {
                return "Kích thước file quá lớn. Tối đa 10MB.";
            }

            // Kiểm tra định dạng file
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(extension))
            {
                return "Định dạng file không được hỗ trợ. Chỉ chấp nhận JPG, PNG, WEBP.";
            }

            // Kiểm tra MIME type
            var allowedMimeTypes = new[] { "image/jpeg", "image/png", "image/webp" };
            if (!allowedMimeTypes.Contains(file.ContentType.ToLowerInvariant()))
            {
                return "Loại file không hợp lệ.";
            }

            // Kiểm tra có phải là ảnh thật không
            try
            {
                using var stream = file.OpenReadStream();
                using var image = SixLabors.ImageSharp.Image.Load(stream);

                // Kiểm tra kích thước tối thiểu
                if (image.Width < 100 || image.Height < 100)
                {
                    return "Kích thước ảnh quá nhỏ. Tối thiểu 100x100 pixels.";
                }
            }
            catch
            {
                return "File không phải là ảnh hợp lệ.";
            }

            return string.Empty;
        }

        private async Task<LeafImage> SaveImageFileAsync(IFormFile file, string userId)
        {
            // Tạo tên file unique với timestamp
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var fileName = $"{timestamp}_{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var uploadPath = Path.Combine(_env.WebRootPath, "uploads");

            // Tạo thư mục nếu chưa có
            if (!Directory.Exists(uploadPath))
            {
                Directory.CreateDirectory(uploadPath);
            }

            var filePath = Path.Combine(uploadPath, fileName);

            // Lưu file
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Tạo record trong database
            var leafImage = new LeafImage
            {
                FilePath = $"/uploads/{fileName}",
                UploadDate = DateTime.UtcNow,
                UserId = userId,
                FileSize = file.Length,
                ImageStatus = "Uploaded",
                OriginalFileName = file.FileName
            };

            _context.LeafImages.Add(leafImage);
            await _context.SaveChangesAsync();

            return leafImage;
        }

        private async Task<byte[]> GetImageBytesAsync(IFormFile file)
        {
            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream);
            return memoryStream.ToArray();
        }

        private async Task AddSymptomsToImageAsync(int leafImageId, List<int> symptomIds, string? notes)
        {
            foreach (var symptomId in symptomIds)
            {
                var leafImageSymptom = new LeafImageSymptom
                {
                    LeafImageId = leafImageId,
                    SymptomId = symptomId,
                    ObservedDate = DateTime.UtcNow,
                    Notes = notes
                };

                _context.LeafImageSymptoms.Add(leafImageSymptom);
            }

            await _context.SaveChangesAsync();
        }

        private async Task CreatePredictionLogAsync(int leafImageId, DateTime requestTime, string status, string? errorMessage = null)
        {
            var log = new PredictionLog
            {
                LeafImageId = leafImageId,
                ModelType = "ResNet50",
                RequestTime = requestTime,
                ResponseTime = DateTime.UtcNow,
                ApiStatus = status,
                ErrorMessage = errorMessage,
                ProcessingTimeMs = (int)(DateTime.UtcNow - requestTime).TotalMilliseconds
            };

            _context.PredictionLogs.Add(log);
            await _context.SaveChangesAsync();
        }

        #endregion
    }
}