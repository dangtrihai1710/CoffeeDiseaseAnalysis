// File: CoffeeDiseaseAnalysis/Controllers/PredictionController.cs - COMPLETE FIXED VERSION
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
        /// Upload ảnh và phân tích bệnh lá cà phê bằng REAL AI MODEL - COMPLETE FIXED
        /// </summary>
        [HttpPost("analyze")]
        public async Task<ActionResult<PredictionResult>> AnalyzeLeafImage([FromForm] UploadImageRequest request)
        {
            // ✅ FIX: Consistent Vietnam timezone cho TẤT CẢ
            var vietnamTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            var startTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vietnamTimeZone); // ✅ Vietnam time
            var vietnamTime = startTime; // ✅ Consistent

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

                // 6. ✅ FIX: Save prediction with Vietnam time
                var prediction = new Prediction
                {
                    LeafImageId = leafImage.Id,
                    DiseaseName = predictionResult.DiseaseName,
                    Confidence = predictionResult.Confidence,
                    SeverityLevel = predictionResult.SeverityLevel,
                    TreatmentSuggestion = predictionResult.TreatmentSuggestion,
                    PredictionDate = vietnamTime // ✅ Vietnam time
                };

                _context.Predictions.Add(prediction);
                await _context.SaveChangesAsync();

                // 7. Cập nhật trạng thái ảnh
                leafImage.ImageStatus = "Processed";
                await _context.SaveChangesAsync();

                // 8. ✅ FIX: Create log with Vietnam time + timezone info
                await CreatePredictionLogAsync(leafImage.Id, startTime, "Success", vietnamTimeZone);

                // 9. Chuẩn bị response
                predictionResult.PredictionId = prediction.Id;
                predictionResult.LeafImageId = leafImage.Id;

                _logger.LogInformation("✅ REAL AI Analysis completed successfully in {ProcessingTime}ms (Vietnam time: {StartVN})",
                    (TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vietnamTimeZone) - startTime).TotalMilliseconds,
                    startTime.ToString("yyyy-MM-dd HH:mm:ss"));

                return Ok(predictionResult);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "❌ AI Model error during analysis");

                // ✅ FIX: Error log cũng dùng Vietnam time
                await CreatePredictionLogAsync(0, startTime, "Failed", vietnamTimeZone, ex.Message);

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

                // ✅ FIX: Error log cũng dùng Vietnam time
                await CreatePredictionLogAsync(0, startTime, "Failed", vietnamTimeZone, ex.Message);

                return StatusCode(500, new
                {
                    Message = "Có lỗi xảy ra khi phân tích ảnh bằng AI",
                    Details = _env.IsDevelopment() ? ex.Message : "Internal server error",
                    StatusCode = 500
                });
            }
        }

        /// <summary>
        /// Phân tích batch nhiều ảnh cùng lúc - COMPLETE FIXED VERSION
        /// </summary>
        [HttpPost("analyze-batch")]
        public async Task<ActionResult<BatchPredictionResponse>> AnalyzeBatch([FromForm] BatchPredictionRequest request)
        {
            // ✅ FIX: Consistent Vietnam timezone cho batch
            var vietnamTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            var startTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vietnamTimeZone);  // ✅ Vietnam time
            var vietnamTime = startTime; // ✅ Consistent timezone

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

                // ===================================================================
                // STEP 1: Validate và lưu tất cả ảnh vào database trước
                // ===================================================================
                var savedImages = new List<(LeafImage leafImage, byte[] bytes)>();

                foreach (var image in request.Images)
                {
                    var validation = ValidateImageFile(image);
                    if (!string.IsNullOrEmpty(validation))
                    {
                        return BadRequest($"Ảnh {image.FileName}: {validation}");
                    }

                    // Lưu ảnh vào database
                    var leafImage = await SaveImageFileAsync(image, user.Id);
                    var bytes = await GetImageBytesAsync(image);

                    savedImages.Add((leafImage, bytes));

                    _logger.LogInformation("✅ Image saved: {ImageId}, Path: {Path}", leafImage.Id, leafImage.FilePath);
                }

                // ===================================================================
                // STEP 2: Cập nhật trạng thái tất cả ảnh thành "Processing"
                // ===================================================================
                foreach (var (leafImage, _) in savedImages)
                {
                    leafImage.ImageStatus = "Processing";
                }
                await _context.SaveChangesAsync();

                // ===================================================================
                // STEP 3: Gọi batch prediction từ AI service
                // ===================================================================
                var imageBytes = savedImages.Select(x => x.bytes).ToList();
                var imagePaths = savedImages.Select(x => x.leafImage.FilePath).ToList();

                var aiResult = await _predictionService.PredictBatchAsync(imageBytes, imagePaths);

                _logger.LogInformation("✅ AI Batch analysis completed: {Processed}/{Total}",
                    aiResult.ProcessedImages, aiResult.TotalImages);

                // ===================================================================
                // STEP 4: ✅ FIX: Lưu kết quả phân tích vào database với Vietnam timezone
                // ===================================================================
                for (int i = 0; i < savedImages.Count && i < aiResult.Results.Count; i++)
                {
                    var leafImage = savedImages[i].leafImage;
                    var predictionResult = aiResult.Results[i];

                    try
                    {
                        // ✅ FIX: Tạo Prediction record với Vietnam time
                        var prediction = new Prediction
                        {
                            LeafImageId = leafImage.Id,
                            DiseaseName = predictionResult.DiseaseName,
                            Confidence = predictionResult.Confidence,
                            SeverityLevel = predictionResult.SeverityLevel,
                            TreatmentSuggestion = predictionResult.TreatmentSuggestion,
                            PredictionDate = vietnamTime,  // ✅ Vietnam time
                            ProcessingTimeMs = predictionResult.ProcessingTimeMs ?? 0
                        };

                        _context.Predictions.Add(prediction);

                        // Cập nhật trạng thái ảnh
                        leafImage.ImageStatus = "Processed";

                        // ✅ FIX: Prediction log với Vietnam time
                        var logTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vietnamTimeZone);

                        var predictionLog = new PredictionLog
                        {
                            LeafImageId = leafImage.Id,
                            ModelType = "ResNet50-RealAI-Batch",
                            RequestTime = startTime,     // ✅ Vietnam time
                            ResponseTime = logTime,      // ✅ Vietnam time
                            ApiStatus = "Success",
                            ProcessingTimeMs = predictionResult.ProcessingTimeMs ?? 0,
                            ModelVersion = "v2.1-Batch"
                        };

                        _context.PredictionLogs.Add(predictionLog);

                        // Set prediction ID trong result để frontend có thể sử dụng
                        predictionResult.PredictionId = prediction.Id;
                        predictionResult.LeafImageId = leafImage.Id;

                        _logger.LogInformation("✅ Saved prediction for image {ImageId}: {Disease} ({Confidence:P2})",
                            leafImage.Id, predictionResult.DiseaseName, predictionResult.Confidence);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "❌ Failed to save prediction for image {ImageId}", leafImage.Id);

                        // Cập nhật trạng thái ảnh thành "Failed" nếu lưu thất bại
                        leafImage.ImageStatus = "Failed";

                        // ✅ FIX: Error log cũng dùng Vietnam time
                        var errorTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vietnamTimeZone);

                        var errorLog = new PredictionLog
                        {
                            LeafImageId = leafImage.Id,
                            ModelType = "ResNet50-RealAI-Batch",
                            RequestTime = startTime,     // ✅ Vietnam time
                            ResponseTime = errorTime,    // ✅ Vietnam time
                            ApiStatus = "Failed",
                            ErrorMessage = ex.Message.Length > 500 ? ex.Message.Substring(0, 500) : ex.Message,
                            ModelVersion = "v2.1-Batch"
                        };

                        _context.PredictionLogs.Add(errorLog);

                        // Thêm lỗi vào batch result
                        aiResult.Errors ??= new List<string>();
                        aiResult.Errors.Add($"Ảnh {leafImage.OriginalFileName}: Lỗi lưu kết quả - {ex.Message}");
                    }
                }

                // ===================================================================
                // STEP 5: Lưu tất cả thay đổi vào database
                // ===================================================================
                await _context.SaveChangesAsync();

                // ===================================================================
                // STEP 6: ✅ FIX: EndTime cũng dùng Vietnam timezone
                // ===================================================================
                var endTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vietnamTimeZone);  // ✅ Vietnam time

                var finalResult = new BatchPredictionResponse
                {
                    TotalImages = aiResult.TotalImages,
                    ProcessedImages = aiResult.ProcessedImages,
                    Results = aiResult.Results,
                    Errors = aiResult.Errors,
                    Status = aiResult.Errors?.Any() == true ? "Partial" : "Completed",
                    StartTime = startTime,        // ✅ Vietnam time
                    EndTime = endTime            // ✅ Vietnam time → TotalProcessingTimeMs sẽ tính đúng
                };

                var totalProcessingTime = finalResult.TotalProcessingTimeMs ?? 0;

                _logger.LogInformation("✅ Batch analysis FULLY completed and saved to database: {Processed}/{Total} successful in {Time}ms (Vietnam time: {StartVN} - {EndVN})",
                    finalResult.ProcessedImages, finalResult.TotalImages, totalProcessingTime,
                    startTime.ToString("yyyy-MM-dd HH:mm:ss"), endTime.ToString("yyyy-MM-dd HH:mm:ss"));

                return Ok(finalResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error during batch analysis");

                // ✅ FIX: Consistent timezone cho error handling
                try
                {
                    var currentUser = await _userManager.GetUserAsync(User);
                    if (currentUser != null)
                    {
                        // Convert UTC startTime back to UTC for database query (vì UploadDate lưu UTC)
                        var utcStartTime = TimeZoneInfo.ConvertTimeToUtc(startTime, vietnamTimeZone);

                        var failedImages = await _context.LeafImages
                            .Where(li => li.UserId == currentUser.Id
                                    && li.UploadDate >= utcStartTime  // ✅ Compare UTC with UTC
                                    && li.ImageStatus == "Processing")
                            .ToListAsync();

                        foreach (var img in failedImages)
                        {
                            img.ImageStatus = "Failed";
                        }

                        await _context.SaveChangesAsync();
                    }
                }
                catch (Exception saveEx)
                {
                    _logger.LogError(saveEx, "❌ Failed to update image status to Failed");
                }

                return StatusCode(500, "Có lỗi xảy ra khi phân tích batch");
            }
        }

        [HttpGet("history")]
        public async Task<ActionResult<object>> GetAnalysisHistory(
           int pageNumber = 1,
           int pageSize = 10,
           string? diseaseFilter = null)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return Unauthorized();

                var query = _context.Predictions
                    .Include(p => p.LeafImage)
                    .Where(p => p.LeafImage.UserId == user.Id);

                if (!string.IsNullOrEmpty(diseaseFilter))
                {
                    query = query.Where(p => p.DiseaseName == diseaseFilter);
                }

                var totalItems = await query.CountAsync();
                var predictions = await query
                    .OrderByDescending(p => p.PredictionDate)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(p => new
                    {
                        p.Id,
                        PredictionId = p.Id,
                        p.LeafImageId,
                        p.DiseaseName,
                        p.Confidence,
                        FinalConfidence = p.FinalConfidence ?? p.Confidence,
                        p.SeverityLevel,
                        p.TreatmentSuggestion,
                        p.PredictionDate,
                        ImagePath = $"{Request.Scheme}://{Request.Host}{p.LeafImage.FilePath}",
                        DetectedSymptoms = p.LeafImage.LeafImageSymptoms
                            .Select(s => s.Symptom.Name).ToList(),
                        ProcessingTimeMs = p.ProcessingTimeMs,
                        IsRealAI = true,
                        ModelVersion = "ResNet50 v2.1"
                    })
                    .ToListAsync();

                return Ok(new
                {
                    data = predictions,
                    totalItems,
                    totalPages = (int)Math.Ceiling((double)totalItems / pageSize)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading history");
                return StatusCode(500, "Error loading history");
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

        #region Private Helper Methods - COMPLETE TIMEZONE FIXED

        /// <summary>
        /// ✅ FIX: CreatePredictionLogAsync với Vietnam Timezone Support
        /// </summary>
        private async Task CreatePredictionLogAsync(int leafImageId, DateTime requestTime, string status,
            TimeZoneInfo timeZone, string? errorMessage = null)
        {
            // ✅ FIX: ResponseTime cũng dùng Vietnam time
            var responseTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);

            var log = new PredictionLog
            {
                LeafImageId = leafImageId,
                ModelType = "ResNet50-RealAI",
                RequestTime = requestTime,       // ✅ Vietnam time
                ResponseTime = responseTime,     // ✅ Vietnam time
                ApiStatus = status,
                ErrorMessage = errorMessage,
                ProcessingTimeMs = (int)(responseTime - requestTime).TotalMilliseconds  // ✅ Correct calculation
            };

            _context.PredictionLogs.Add(log);
            await _context.SaveChangesAsync();

            _logger.LogInformation("✅ Created prediction log with Vietnam time: Request={RequestTime}, Response={ResponseTime}",
                requestTime.ToString("yyyy-MM-dd HH:mm:ss"), responseTime.ToString("yyyy-MM-dd HH:mm:ss"));
        }

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

        #endregion
    }
}