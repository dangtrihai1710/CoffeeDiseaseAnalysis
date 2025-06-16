// File: CoffeeDiseaseAnalysis/Controllers/PredictionController.cs - Updated Version
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
        private readonly IPredictionService _predictionService;
        private readonly IMessageQueueService _messageQueueService;
        private readonly ICacheService _cacheService;
        private readonly ILogger<PredictionController> _logger;
        private readonly IWebHostEnvironment _env;

        public PredictionController(
            ApplicationDbContext context,
            UserManager<User> userManager,
            IPredictionService predictionService,
            IMessageQueueService messageQueueService,
            ICacheService cacheService,
            ILogger<PredictionController> logger,
            IWebHostEnvironment env)
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
        /// Upload ảnh lá cà phê để phân tích bệnh (Synchronous)
        /// </summary>
        [HttpPost("upload")]
        public async Task<ActionResult<PredictionResult>> UploadImage([FromForm] UploadImageRequest request)
        {
            try
            {
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

                // Lưu file và tạo LeafImage record
                var leafImage = await SaveImageFileAsync(request.Image, user.Id);

                // Thêm triệu chứng nếu có
                if (request.SymptomIds?.Any() == true)
                {
                    await AddSymptomsToImageAsync(leafImage.Id, request.SymptomIds, user.Id, request.Notes);
                }

                // Đọc image bytes để predict
                var imageBytes = await GetImageBytesAsync(request.Image);

                // Thực hiện prediction đồng bộ
                var predictionResult = await _predictionService.PredictDiseaseAsync(
                    imageBytes, leafImage.FilePath, request.SymptomIds);

                // Lưu prediction vào database
                await SavePredictionAsync(leafImage.Id, predictionResult);

                // Cập nhật trạng thái ảnh
                leafImage.ImageStatus = "Processed";
                await _context.SaveChangesAsync();

                return Ok(predictionResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi upload và phân tích ảnh đồng bộ");
                return StatusCode(500, "Có lỗi xảy ra khi xử lý ảnh");
            }
        }

        /// <summary>
        /// Upload ảnh lá cà phê để phân tích bệnh (Asynchronous với RabbitMQ)
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi upload ảnh bất đồng bộ");
                return StatusCode(500, "Có lỗi xảy ra khi xử lý ảnh");
            }
        }

        /// <summary>
        /// Upload nhiều ảnh để phân tích batch
        /// </summary>
        [HttpPost("upload-batch")]
        public async Task<ActionResult<BatchPredictionResponse>> UploadBatch([FromForm] BatchPredictionRequest request)
        {
            try
            {
                if (request.Images?.Any() != true)
                {
                    return BadRequest("Không có ảnh nào được upload");
                }

                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return Unauthorized();
                }

                var imageBytesList = new List<byte[]>();
                var imagePaths = new List<string>();
                var leafImages = new List<LeafImage>();

                // Xử lý từng ảnh
                foreach (var image in request.Images)
                {
                    var validationResult = ValidateImageFile(image);
                    if (!string.IsNullOrEmpty(validationResult))
                    {
                        continue; // Skip invalid images
                    }

                    var leafImage = await SaveImageFileAsync(image, user.Id);
                    var imageBytes = await GetImageBytesAsync(image);

                    leafImages.Add(leafImage);
                    imageBytesList.Add(imageBytes);
                    imagePaths.Add(leafImage.FilePath);
                }

                if (!imageBytesList.Any())
                {
                    return BadRequest("Không có ảnh hợp lệ nào để xử lý");
                }

                // Thực hiện batch prediction
                var batchResult = await _predictionService.PredictBatchAsync(imageBytesList, imagePaths);

                // Lưu predictions vào database
                for (int i = 0; i < batchResult.Results.Count; i++)
                {
                    if (i < leafImages.Count)
                    {
                        await SavePredictionAsync(leafImages[i].Id, batchResult.Results[i]);
                        leafImages[i].ImageStatus = "Processed";
                    }
                }

                await _context.SaveChangesAsync();

                return Ok(batchResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xử lý batch upload");
                return StatusCode(500, "Có lỗi xảy ra khi xử lý batch ảnh");
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
                        p.Feedbacks.FirstOrDefault()!.Rating : null,
                    ImageStatus = p.LeafImage.ImageStatus
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

        /// <summary>
        /// Lấy chi tiết một prediction
        /// </summary>
        [HttpGet("{predictionId}")]
        public async Task<ActionResult<object>> GetPredictionDetail(int predictionId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            var prediction = await _context.Predictions
                .Include(p => p.LeafImage)
                .ThenInclude(l => l.LeafImageSymptoms)
                .ThenInclude(s => s.Symptom)
                .Include(p => p.Feedbacks)
                .FirstOrDefaultAsync(p => p.Id == predictionId && p.LeafImage.UserId == user.Id);

            if (prediction == null)
            {
                return NotFound("Không tìm thấy prediction");
            }

            var result = new
            {
                prediction.Id,
                prediction.DiseaseName,
                prediction.Confidence,
                prediction.FinalConfidence,
                prediction.ModelVersion,
                prediction.SeverityLevel,
                prediction.TreatmentSuggestion,
                prediction.PredictionDate,
                prediction.ProcessingTimeMs,
                Image = new
                {
                    prediction.LeafImage.Id,
                    prediction.LeafImage.FilePath,
                    prediction.LeafImage.UploadDate,
                    prediction.LeafImage.ImageStatus,
                    prediction.LeafImage.FileSize
                },
                Symptoms = prediction.LeafImage.LeafImageSymptoms.Select(s => new
                {
                    s.Symptom.Id,
                    s.Symptom.Name,
                    s.Symptom.Description,
                    s.Symptom.Category,
                    s.Intensity,
                    s.Notes
                }).ToList(),
                Feedbacks = prediction.Feedbacks.Select(f => new
                {
                    f.Id,
                    f.Rating,
                    f.FeedbackText,
                    f.CorrectDiseaseName,
                    f.FeedbackDate
                }).ToList()
            };

            return Ok(result);
        }

        /// <summary>
        /// Kiểm tra trạng thái xử lý ảnh async
        /// </summary>
        [HttpGet("status/{leafImageId}")]
        public async Task<ActionResult<object>> GetProcessingStatus(int leafImageId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            var leafImage = await _context.LeafImages
                .Include(l => l.Predictions)
                .Include(l => l.PredictionLogs)
                .FirstOrDefaultAsync(l => l.Id == leafImageId && l.UserId == user.Id);

            if (leafImage == null)
            {
                return NotFound("Không tìm thấy ảnh");
            }

            var latestPrediction = leafImage.Predictions.OrderByDescending(p => p.PredictionDate).FirstOrDefault();
            var latestLog = leafImage.PredictionLogs.OrderByDescending(l => l.RequestTime).FirstOrDefault();

            return Ok(new
            {
                LeafImageId = leafImage.Id,
                Status = leafImage.ImageStatus,
                UploadDate = leafImage.UploadDate,
                Prediction = latestPrediction != null ? new
                {
                    latestPrediction.Id,
                    latestPrediction.DiseaseName,
                    latestPrediction.Confidence,
                    latestPrediction.PredictionDate,
                    latestPrediction.SeverityLevel
                } : null,
                ProcessingLog = latestLog != null ? new
                {
                    latestLog.RequestTime,
                    latestLog.ResponseTime,
                    latestLog.ApiStatus,
                    latestLog.ProcessingTimeMs,
                    latestLog.ErrorMessage
                } : null
            });
        }

        /// <summary>
        /// Thêm phản hồi cho dự đoán
        /// </summary>
        [HttpPost("feedback")]
        public async Task<ActionResult<FeedbackResponse>> AddFeedback([FromBody] FeedbackRequest request)
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
                    .FirstOrDefaultAsync(p => p.Id == request.PredictionId &&
                                           p.LeafImage.UserId == user.Id);

                if (prediction == null)
                {
                    return NotFound("Không tìm thấy dự đoán");
                }

                var feedback = new Feedback
                {
                    PredictionId = request.PredictionId,
                    UserId = user.Id,
                    Rating = request.Rating,
                    FeedbackText = request.FeedbackText,
                    CorrectDiseaseName = request.CorrectDiseaseName
                };

                _context.Feedbacks.Add(feedback);
                await _context.SaveChangesAsync();

                // Nếu rating thấp, thêm vào training data để cải thiện model
                if (request.Rating <= 2 && !string.IsNullOrEmpty(request.CorrectDiseaseName))
                {
                    var trainingData = new TrainingData
                    {
                        LeafImageId = prediction.LeafImageId,
                        Label = request.CorrectDiseaseName,
                        Source = "Feedback",
                        FeedbackId = feedback.Id,
                        OriginalPrediction = prediction.DiseaseName,
                        OriginalConfidence = prediction.Confidence,
                        Quality = request.Rating <= 1 ? "High" : "Medium" // High quality correction cho rating = 1
                    };

                    _context.TrainingDataRecords.Add(trainingData);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Added training data from feedback. Prediction: {Original} -> Correct: {Correct}, Rating: {Rating}",
                        prediction.DiseaseName, request.CorrectDiseaseName, request.Rating);
                }

                var response = new FeedbackResponse
                {
                    Id = feedback.Id,
                    Rating = feedback.Rating,
                    FeedbackText = feedback.FeedbackText,
                    CorrectDiseaseName = feedback.CorrectDiseaseName,
                    FeedbackDate = feedback.FeedbackDate,
                    IsUsedForTraining = request.Rating <= 2 && !string.IsNullOrEmpty(request.CorrectDiseaseName)
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thêm phản hồi");
                return StatusCode(500, "Có lỗi xảy ra khi thêm phản hồi");
            }
        }

        /// <summary>
        /// Lấy danh sách triệu chứng
        /// </summary>
        [HttpGet("symptoms")]
        public async Task<ActionResult<List<SymptomDto>>> GetSymptoms(string? category = null)
        {
            var query = _context.Symptoms.Where(s => s.IsActive);

            if (!string.IsNullOrEmpty(category))
            {
                query = query.Where(s => s.Category == category);
            }

            var symptoms = await query
                .OrderBy(s => s.Category)
                .ThenBy(s => s.Name)
                .Select(s => new SymptomDto
                {
                    Id = s.Id,
                    Name = s.Name,
                    Description = s.Description,
                    Category = s.Category,
                    Weight = s.Weight
                })
                .ToListAsync();

            return Ok(symptoms);
        }

        /// <summary>
        /// Lấy thống kê model hiện tại
        /// </summary>
        [HttpGet("model-stats")]
        public async Task<ActionResult<ModelStatistics>> GetModelStatistics()
        {
            try
            {
                var stats = await _predictionService.GetCurrentModelInfoAsync();
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy thống kê model");
                return StatusCode(500, "Có lỗi xảy ra khi lấy thống kê model");
            }
        }

        #region Private Methods

        private string ValidateImageFile(IFormFile file)
        {
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(fileExtension))
            {
                return "Chỉ hỗ trợ file JPG và PNG";
            }

            if (file.Length > 10 * 1024 * 1024) // 10MB
            {
                return "Kích thước file không được vượt quá 10MB";
            }

            if (file.Length < 1024) // 1KB
            {
                return "File quá nhỏ, có thể bị lỗi";
            }

            return string.Empty;
        }

        private async Task<LeafImage> SaveImageFileAsync(IFormFile file, string userId)
        {
            var uploadDir = Path.Combine(_env.WebRootPath, "uploads");
            if (!Directory.Exists(uploadDir))
            {
                Directory.CreateDirectory(uploadDir);
            }

            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var fileName = $"{Guid.NewGuid()}{fileExtension}";
            var filePath = Path.Combine(uploadDir, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Tính hash và kích thước ảnh
            var imageBytes = await GetImageBytesAsync(file);
            var imageHash = CalculateImageHash(imageBytes);

            // Lấy kích thước ảnh
            using var image = SixLabors.ImageSharp.Image.Load(imageBytes);

            var leafImage = new LeafImage
            {
                FilePath = $"/uploads/{fileName}",
                UserId = userId,
                FileSize = file.Length,
                ImageHash = imageHash,
                FileExtension = fileExtension,
                Width = image.Width,
                Height = image.Height,
                ImageStatus = "Pending"
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

        private string CalculateImageHash(byte[] imageBytes)
        {
            using var md5 = MD5.Create();
            var hash = md5.ComputeHash(imageBytes);
            return Convert.ToHexString(hash);
        }

        private async Task AddSymptomsToImageAsync(int leafImageId, List<int> symptomIds, string userId, string? notes)
        {
            var symptoms = await _context.Symptoms
                .Where(s => symptomIds.Contains(s.Id) && s.IsActive)
                .ToListAsync();

            foreach (var symptom in symptoms)
            {
                _context.LeafImageSymptoms.Add(new LeafImageSymptom
                {
                    LeafImageId = leafImageId,
                    SymptomId = symptom.Id,
                    ObservedByUserId = userId,
                    Notes = notes,
                    Intensity = 3 // Default intensity
                });
            }

            await _context.SaveChangesAsync();
        }

        private async Task SavePredictionAsync(int leafImageId, PredictionResult result)
        {
            var prediction = new Prediction
            {
                LeafImageId = leafImageId,
                DiseaseName = result.DiseaseName,
                Confidence = result.Confidence,
                FinalConfidence = result.FinalConfidence,
                ModelVersion = result.ModelVersion,
                SeverityLevel = result.SeverityLevel,
                TreatmentSuggestion = result.TreatmentSuggestion,
                ProcessingTimeMs = result.ProcessingTimeMs
            };

            _context.Predictions.Add(prediction);
            await _context.SaveChangesAsync();

            // Update result ID
            result.Id = prediction.Id;
        }

        #endregion
    }
}