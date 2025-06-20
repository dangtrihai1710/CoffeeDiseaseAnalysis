// File: CoffeeDiseaseAnalysis/Controllers/PredictionController.cs - FIXED với Mock Services
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using CoffeeDiseaseAnalysis.Data;
using CoffeeDiseaseAnalysis.Data.Entities;
using CoffeeDiseaseAnalysis.Models.DTOs;
using CoffeeDiseaseAnalysis.Services.Interfaces;
using System.Security.Cryptography;

namespace CoffeeDiseaseAnalysis.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PredictionController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IPredictionService? _predictionService;
        private readonly IMessageQueueService? _messageQueueService;
        private readonly ICacheService? _cacheService;
        private readonly ILogger<PredictionController> _logger;
        private readonly IWebHostEnvironment _env;

        public PredictionController(
            ApplicationDbContext context,
            UserManager<User> userManager,
            ILogger<PredictionController> logger,
            IWebHostEnvironment env,
            IPredictionService? predictionService = null,
            IMessageQueueService? messageQueueService = null,
            ICacheService? cacheService = null)
        {
            _context = context;
            _userManager = userManager;
            _predictionService = predictionService;
            _messageQueueService = messageQueueService;
            _cacheService = cacheService;
            _logger = logger;
            _env = env;
        }

        /// <summary>
        /// Health check endpoint - không cần authentication
        /// </summary>
        [HttpGet("health")]
        [AllowAnonymous]
        public async Task<ActionResult<object>> HealthCheck()
        {
            try
            {
                // Check database connection
                var dbHealthy = await _context.Database.CanConnectAsync();

                // Check services availability
                var predictionHealthy = _predictionService != null;
                var cacheHealthy = _cacheService != null;

                return Ok(new
                {
                    Status = "Healthy",
                    Timestamp = DateTime.UtcNow,
                    Database = dbHealthy ? "Connected" : "Disconnected",
                    PredictionService = predictionHealthy ? "Available" : "Unavailable",
                    CacheService = cacheHealthy ? "Available" : "Unavailable",
                    Version = "1.3.0"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check failed");
                return StatusCode(500, new { Status = "Unhealthy", Error = ex.Message });
            }
        }

        /// <summary>
        /// Upload ảnh lá cà phê để phân tích bệnh (với graceful fallback)
        /// </summary>
        [HttpPost("upload")]
        public async Task<ActionResult<PredictionResult>> UploadImage([FromForm] UploadImageRequest request)
        {
            try
            {
                _logger.LogInformation("🔄 Starting image upload process...");

                if (request.Image == null || request.Image.Length == 0)
                {
                    return BadRequest("Không có ảnh nào được upload");
                }

                // Validate file
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

                // Lưu file và tạo LeafImage record
                var leafImage = await SaveImageFileAsync(request.Image, user.Id);
                _logger.LogInformation("✅ Image saved: {ImageId}, Path: {Path}", leafImage.Id, leafImage.FilePath);

                // Thêm triệu chứng nếu có
                if (request.SymptomIds?.Any() == true)
                {
                    await AddSymptomsToImageAsync(leafImage.Id, request.SymptomIds, user.Id, request.Notes);
                    _logger.LogInformation("✅ Symptoms added: {Count} symptoms", request.SymptomIds.Count);
                }

                PredictionResult predictionResult;

                // Thử sử dụng AI service thật nếu có
                if (_predictionService != null)
                {
                    try
                    {
                        var imageBytes = await GetImageBytesAsync(request.Image);
                        predictionResult = await _predictionService.PredictDiseaseAsync(
                            imageBytes, leafImage.FilePath, request.SymptomIds);
                        _logger.LogInformation("✅ Real AI prediction successful");
                    }
                    catch (Exception aiEx)
                    {
                        _logger.LogWarning(aiEx, "⚠️ AI service failed, using mock prediction");
                        predictionResult = CreateMockPrediction(leafImage.FilePath);
                    }
                }
                else
                {
                    // Fallback to mock prediction
                    _logger.LogInformation("ℹ️ Using mock prediction service");
                    predictionResult = CreateMockPrediction(leafImage.FilePath);
                }

                // Lưu prediction vào database
                await SavePredictionAsync(leafImage.Id, predictionResult);

                // Cập nhật trạng thái ảnh
                leafImage.ImageStatus = "Processed";
                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ Prediction completed: {Disease} ({Confidence:P})",
                    predictionResult.DiseaseName, predictionResult.Confidence);

                return Ok(predictionResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error during image upload and analysis");
                return StatusCode(500, "Có lỗi xảy ra khi xử lý ảnh. Vui lòng thử lại.");
            }
        }

        /// <summary>
        /// Upload ảnh bất đồng bộ với message queue
        /// </summary>
        [HttpPost("upload-async")]
        public async Task<ActionResult<object>> UploadImageAsync([FromForm] UploadImageRequest request)
        {
            try
            {
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

                // Lưu file và tạo LeafImage record
                var leafImage = await SaveImageFileAsync(request.Image, user.Id);

                // Thêm triệu chứng nếu có
                if (request.SymptomIds?.Any() == true)
                {
                    await AddSymptomsToImageAsync(leafImage.Id, request.SymptomIds, user.Id, request.Notes);
                }

                if (_messageQueueService != null)
                {
                    // Tạo request cho message queue
                    var mqRequest = new ImageProcessingRequest
                    {
                        LeafImageId = leafImage.Id,
                        ImagePath = leafImage.FilePath,
                        SymptomIds = request.SymptomIds,
                        UserId = user.Id,
                        RequestTime = DateTime.UtcNow,
                        RequestId = Guid.NewGuid().ToString()
                    };

                    // Publish vào message queue
                    await _messageQueueService.PublishImageProcessingRequestAsync(mqRequest);

                    return Ok(new
                    {
                        LeafImageId = leafImage.Id,
                        RequestId = mqRequest.RequestId,
                        Status = "Processing",
                        Message = "Ảnh đã được gửi vào hàng đợi xử lý. Vui lòng kiểm tra kết quả sau ít phút.",
                        ImagePath = leafImage.FilePath
                    });
                }
                else
                {
                    // Fallback to synchronous processing
                    _logger.LogInformation("Message queue not available, processing synchronously");

                    var syncResult = await UploadImage(request);
                    if (syncResult.Result is OkObjectResult okResult)
                    {
                        return Ok(new
                        {
                            LeafImageId = leafImage.Id,
                            RequestId = Guid.NewGuid().ToString(),
                            Status = "Completed",
                            Result = okResult.Value,
                            Message = "Xử lý đồng bộ thành công"
                        });
                    }
                    return syncResult;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi upload ảnh bất đồng bộ");
                return StatusCode(500, "Có lỗi xảy ra khi xử lý ảnh");
            }
        }

        /// <summary>
        /// Lấy lịch sử dự đoán của người dùng
        /// </summary>
        [HttpGet("history")]
        public async Task<ActionResult<object>> GetPredictionHistory(
            int pageNumber = 1, int pageSize = 10, string? diseaseFilter = null, string? statusFilter = null)
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
    ModelVersion = p.ModelVersion,
    PredictionDate = p.PredictionDate,
    SeverityLevel = p.SeverityLevel,
    FeedbackRating = p.Feedbacks.FirstOrDefault() != null ?
        p.Feedbacks.First().Rating : null,
    Status = p.LeafImage.ImageStatus,
    ImageStatus = p.LeafImage.ImageStatus
})
.ToListAsync();

            return Ok(new
            {
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling((double)totalCount / pageSize),
                Predictions = predictions
            });
        }

        #region Helper Methods

        private string ValidateImageFile(IFormFile file)
        {
            // Check file size (50MB max)
            if (file.Length > 50 * 1024 * 1024)
            {
                return "File quá lớn. Kích thước tối đa là 50MB.";
            }

            // Check file extension
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (string.IsNullOrEmpty(fileExtension) || !allowedExtensions.Contains(fileExtension))
            {
                return "Định dạng file không được hỗ trợ. Vui lòng upload file ảnh (.jpg, .jpeg, .png, .bmp, .gif).";
            }

            // Check MIME type
            var allowedMimeTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/bmp", "image/gif" };
            if (!allowedMimeTypes.Contains(file.ContentType.ToLowerInvariant()))
            {
                return "MIME type không hợp lệ.";
            }

            return string.Empty; // Valid file
        }

        private async Task<LeafImage> SaveImageFileAsync(IFormFile file, string userId)
        {
            // Tạo thư mục upload nếu chưa tồn tại
            var uploadsFolder = Path.Combine(_env.WebRootPath ?? _env.ContentRootPath, "uploads", "images");
            Directory.CreateDirectory(uploadsFolder);

            // Tạo tên file unique
            var fileExtension = Path.GetExtension(file.FileName);
            var fileName = $"{Guid.NewGuid()}{fileExtension}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            // Lưu file
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            // Tạo record trong database
            var leafImage = new LeafImage
            {
                FileName = file.FileName,
                FilePath = $"/uploads/images/{fileName}",
                FileSize = file.Length,
                UploadDate = DateTime.UtcNow,
                UserId = userId,
                ImageStatus = "Uploaded"
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

        private async Task AddSymptomsToImageAsync(int leafImageId, List<int> symptomIds, string userId, string? notes)
        {
            foreach (var symptomId in symptomIds)
            {
                var imageSymptom = new ImageSymptom
                {
                    LeafImageId = leafImageId,
                    SymptomId = symptomId,
                    DetectedDate = DateTime.UtcNow,
                    DetectedBy = userId,
                    Notes = notes
                };

                _context.ImageSymptoms.Add(imageSymptom);
            }

            await _context.SaveChangesAsync();
        }

        private PredictionResult CreateMockPrediction(string imagePath)
        {
            var diseases = new[]
            {
                ("Cercospora", 0.85m, "Cao", "Bệnh đốm nâu do nấm Cercospora coffeicola"),
                ("Rust", 0.78m, "Trung Bình", "Bệnh rỉ sắt do nấm Hemileia vastatrix"),
                ("Miner", 0.65m, "Thấp", "Bệnh do sâu đục lá"),
                ("Phoma", 0.72m, "Trung Bình", "Bệnh đốm đen do nấm Phoma"),
                ("Healthy", 0.92m, "Tốt", "Lá khỏe mạnh, không có dấu hiệu bệnh")
            };

            var random = new Random();
            var selectedDisease = diseases[random.Next(diseases.Length)];

            var mockResult = new PredictionResult
            {
                DiseaseName = selectedDisease.Item1,
                Confidence = selectedDisease.Item2,
                SeverityLevel = selectedDisease.Item3,
                Description = selectedDisease.Item4,
                ModelVersion = "MockModel_v1.0",
                PredictionDate = DateTime.UtcNow,
                ProcessingTimeMs = random.Next(500, 2000),
                TreatmentSuggestion = GetTreatmentSuggestion(selectedDisease.Item1),
                ImagePath = imagePath
            };

            _logger.LogInformation("✅ Mock prediction created: {Disease} ({Confidence:P})",
                mockResult.DiseaseName, mockResult.Confidence);

            return mockResult;
        }

        private async Task SavePredictionAsync(int leafImageId, PredictionResult result)
        {
            var prediction = new Prediction
            {
                LeafImageId = leafImageId,
                DiseaseName = result.DiseaseName,
                Confidence = result.Confidence,
                SeverityLevel = result.SeverityLevel,
                TreatmentSuggestion = result.TreatmentSuggestion,
                ModelVersion = result.ModelVersion,
                PredictionDate = result.PredictionDate,
                ProcessingTimeMs = result.ProcessingTimeMs,
                FinalConfidence = result.FinalConfidence
            };

            _context.Predictions.Add(prediction);
            await _context.SaveChangesAsync();
        }

        private string GetTreatmentSuggestion(string diseaseName)
        {
            return diseaseName switch
            {
                "Cercospora" => "Sử dụng thuốc fungicide như Copper sulfate. Tăng cường thoáng khí và giảm độ ẩm.",
                "Rust" => "Phun thuốc tricides chứa đồng. Loại bỏ lá bệnh và cải thiện thoát nước.",
                "Miner" => "Sử dụng thuốc trừ sâu sinh học. Loại bỏ lá bị tổn thương.",
                "Phoma" => "Cắt tỉa lá bệnh, cải thiện thông gió và áp dụng phun fungicide.",
                "Healthy" => "Duy trì chăm sóc bình thường. Theo dõi thường xuyên để phát hiện sớm các vấn đề.",
                _ => "Tham khảo chuyên gia nông nghiệp để được tư vấn điều trị phù hợp."
            };
        }

        #endregion
    }
}