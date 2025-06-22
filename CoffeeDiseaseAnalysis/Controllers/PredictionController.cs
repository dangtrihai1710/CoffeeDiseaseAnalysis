// File: CoffeeDiseaseAnalysis/Controllers/PredictionController.cs - UPDATED FOR REAL AI
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
        /// Upload ảnh và phân tích bệnh lá cà phê bằng REAL AI MODEL
        /// </summary>
        [HttpPost("analyze")]
        public async Task<ActionResult<PredictionResult>> AnalyzeLeafImage([FromForm] UploadImageRequest request)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                _logger.LogInformation("🔄 Starting REAL AI leaf image analysis...");

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

                // 2. Kiểm tra AI model có sẵn không
                var isModelAvailable = await _predictionService.IsModelAvailableAsync();
                if (!isModelAvailable)
                {
                    return StatusCode(503, new
                    {
                        Message = "AI Model không khả dụng",
                        Details = "Vui lòng đảm bảo file coffee_resnet50_model_final.onnx có trong thư mục wwwroot/models/",
                        StatusCode = 503
                    });
                }

                _logger.LogInformation("✅ User authenticated: {UserId}, AI Model available", user.Id);

                // 3. Lưu ảnh vào thư mục và database
                var leafImage = await SaveImageFileAsync(request.Image, user.Id);
                _logger.LogInformation("✅ Image saved: {ImageId}, Path: {Path}", leafImage.Id, leafImage.FilePath);

                // 4. Thêm triệu chứng nếu có
                if (request.SymptomIds?.Any() == true)
                {
                    await AddSymptomsToImageAsync(leafImage.Id, request.SymptomIds, request.Notes);
                    _logger.LogInformation("✅ Symptoms added: {Count} symptoms", request.SymptomIds.Count);
                }

                // 5. Gọi REAL AI model để phân tích
                leafImage.ImageStatus = "Processing";
                await _context.SaveChangesAsync();

                var imageBytes = await GetImageBytesAsync(request.Image);

                _logger.LogInformation("🤖 Calling REAL AI model for prediction...");
                var predictionResult = await _predictionService.PredictDiseaseAsync(
                    imageBytes, leafImage.FilePath, request.SymptomIds);

                _logger.LogInformation("✅ REAL AI prediction completed: {Disease} ({Confidence:P2})",
                    predictionResult.DiseaseName, predictionResult.Confidence);

                // 6. Lưu kết quả prediction vào database
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

                // 7. Cập nhật trạng thái ảnh
                leafImage.ImageStatus = "Processed";
                await _context.SaveChangesAsync();

                // 8. Tạo prediction log
                await CreatePredictionLogAsync(leafImage.Id, startTime, "Success");

                // 9. Chuẩn bị response
                predictionResult.PredictionId = prediction.Id;
                predictionResult.LeafImageId = leafImage.Id;

                _logger.LogInformation("✅ REAL AI Analysis completed successfully in {ProcessingTime}ms",
                    (DateTime.UtcNow - startTime).TotalMilliseconds);

                return Ok(predictionResult);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "❌ AI Model error during analysis");
                return StatusCode(503, new
                {
                    Message = "Lỗi AI Model",
                    Details = ex.Message,
                    StatusCode = 503
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Unexpected error during REAL AI analysis");
                return StatusCode(500, new
                {
                    Message = "Có lỗi xảy ra khi phân tích ảnh bằng AI",
                    Details = _env.IsDevelopment() ? ex.Message : "Internal server error",
                    StatusCode = 500
                });
            }
        }

        /// <summary>
        /// Phân tích batch nhiều ảnh cùng lúc
        /// </summary>
        [HttpPost("analyze-batch")]
        public async Task<ActionResult<BatchPredictionResponse>> AnalyzeBatch([FromForm] BatchPredictionRequest request)
        {
            try
            {
                _logger.LogInformation("🔄 Starting batch analysis for {Count} images", request.Images.Count);

                if (request.Images?.Count == 0)
                {
                    return BadRequest("Không có ảnh nào được upload");
                }

                if (request.Images.Count > 10)
                {
                    return BadRequest("Tối đa 10 ảnh mỗi batch");
                }

                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return Unauthorized();
                }

                // Kiểm tra AI model
                var isModelAvailable = await _predictionService.IsModelAvailableAsync();
                if (!isModelAvailable)
                {
                    return StatusCode(503, "AI Model không khả dụng");
                }

                // Validate tất cả ảnh trước
                var imageData = new List<(byte[] bytes, string path)>();

                foreach (var image in request.Images)
                {
                    var validation = ValidateImageFile(image);
                    if (!string.IsNullOrEmpty(validation))
                    {
                        return BadRequest($"Ảnh {image.FileName}: {validation}");
                    }

                    var leafImage = await SaveImageFileAsync(image, user.Id);
                    var bytes = await GetImageBytesAsync(image);
                    imageData.Add((bytes, leafImage.FilePath));
                }

                // Gọi batch prediction
                var batchResult = await _predictionService.PredictBatchAsync(
                    imageData.Select(x => x.bytes).ToList(),
                    imageData.Select(x => x.path).ToList()
                );

                _logger.LogInformation("✅ Batch analysis completed: {Processed}/{Total}",
                    batchResult.ProcessedImages, batchResult.TotalImages);

                return Ok(batchResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error during batch analysis");
                return StatusCode(500, "Có lỗi xảy ra khi phân tích batch");
            }
        }

        /// <summary>
        /// Lấy lịch sử phân tích của người dùng
        /// </summary>
        [HttpGet("history")]
        public async Task<ActionResult<object>> GetAnalysisHistory(
            int pageNumber = 1,
            int pageSize = 10,
            string? diseaseFilter = null)
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

                if (!string.IsNullOrEmpty(diseaseFilter))
                {
                    query = query.Where(p => p.DiseaseName.Contains(diseaseFilter));
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
                        TreatmentSuggestion = p.TreatmentSuggestion,
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
                    TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting prediction history");
                return StatusCode(500, "Có lỗi xảy ra khi lấy lịch sử phân tích");
            }
        }

        /// <summary>
        /// Health check - Kiểm tra AI model
        /// </summary>
        [HttpGet("health")]
        [AllowAnonymous]
        public async Task<ActionResult<object>> HealthCheck()
        {
            try
            {
                var dbHealthy = await _context.Database.CanConnectAsync();
                var modelAvailable = await _predictionService.IsModelAvailableAsync();
                var modelStats = await _predictionService.GetModelStatsAsync();

                var status = dbHealthy && modelAvailable ? "Healthy" : "Degraded";

                return Ok(new
                {
                    Status = status,
                    Timestamp = DateTime.UtcNow,
                    Services = new
                    {
                        Database = dbHealthy ? "Connected" : "Disconnected",
                        AIModel = modelAvailable ? "Available" : "Unavailable"
                    },
                    ModelInfo = modelStats,
                    Version = "2.1.0-RealAI",
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

        #region Private Helper Methods

        private string ValidateImageFile(IFormFile file)
        {
            if (file.Length > 10 * 1024 * 1024)
            {
                return "Kích thước file quá lớn. Tối đa 10MB.";
            }

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(extension))
            {
                return "Định dạng file không được hỗ trợ. Chỉ chấp nhận JPG, PNG, WEBP.";
            }

            var allowedMimeTypes = new[] { "image/jpeg", "image/png", "image/webp" };
            if (!allowedMimeTypes.Contains(file.ContentType.ToLowerInvariant()))
            {
                return "Loại file không hợp lệ.";
            }

            try
            {
                using var stream = file.OpenReadStream();
                using var image = SixLabors.ImageSharp.Image.Load(stream);

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
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var fileName = $"{timestamp}_{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var uploadPath = Path.Combine(_env.WebRootPath, "uploads");

            if (!Directory.Exists(uploadPath))
            {
                Directory.CreateDirectory(uploadPath);
            }

            var filePath = Path.Combine(uploadPath, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

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
                ModelType = "ResNet50-RealAI",
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