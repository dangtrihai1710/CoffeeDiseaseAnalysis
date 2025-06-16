using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using CoffeeDiseaseAnalysis.Data;
using CoffeeDiseaseAnalysis.Data.Entities;
using CoffeeDiseaseAnalysis.Models.DTOs;
using System.Security.Cryptography;
using System.Text;

namespace CoffeeDiseaseAnalysis.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PredictionController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly ILogger<PredictionController> _logger;
        private readonly IWebHostEnvironment _env;

        public PredictionController(
            ApplicationDbContext context,
            UserManager<User> userManager,
            ILogger<PredictionController> logger,
            IWebHostEnvironment env)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _env = env;
        }

        /// <summary>
        /// Upload ảnh lá cà phê để phân tích bệnh
        /// </summary>
        /// <param name="request">Thông tin upload ảnh</param>
        /// <returns>Kết quả dự đoán bệnh</returns>
        [HttpPost("upload")]
        public async Task<ActionResult<PredictionResult>> UploadImage([FromForm] UploadImageRequest request)
        {
            try
            {
                if (request.Image == null || request.Image.Length == 0)
                {
                    return BadRequest("Không có ảnh nào được upload");
                }

                // Kiểm tra định dạng file
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
                var fileExtension = Path.GetExtension(request.Image.FileName).ToLowerInvariant();

                if (!allowedExtensions.Contains(fileExtension))
                {
                    return BadRequest("Chỉ hỗ trợ file JPG và PNG");
                }

                // Kiểm tra kích thước file (max 10MB)
                if (request.Image.Length > 10 * 1024 * 1024)
                {
                    return BadRequest("Kích thước file không được vượt quá 10MB");
                }

                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return Unauthorized();
                }

                // Tạo thư mục upload nếu chưa tồn tại
                var uploadDir = Path.Combine(_env.WebRootPath, "uploads");
                if (!Directory.Exists(uploadDir))
                {
                    Directory.CreateDirectory(uploadDir);
                }

                // Tạo tên file unique
                var fileName = $"{Guid.NewGuid()}{fileExtension}";
                var filePath = Path.Combine(uploadDir, fileName);

                // Lưu file
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await request.Image.CopyToAsync(stream);
                }

                // Tính hash MD5 cho ảnh
                var imageHash = await CalculateImageHashAsync(filePath);

                // Tạo bản ghi LeafImage
                var leafImage = new LeafImage
                {
                    FilePath = $"/uploads/{fileName}",
                    UserId = user.Id,
                    FileSize = request.Image.Length,
                    ImageHash = imageHash,
                    FileExtension = fileExtension,
                    ImageStatus = "Pending"
                };

                _context.LeafImages.Add(leafImage);
                await _context.SaveChangesAsync();

                // Thêm triệu chứng nếu có
                if (request.SymptomIds?.Any() == true)
                {
                    var symptoms = await _context.Symptoms
                        .Where(s => request.SymptomIds.Contains(s.Id) && s.IsActive)
                        .ToListAsync();

                    foreach (var symptom in symptoms)
                    {
                        _context.LeafImageSymptoms.Add(new LeafImageSymptom
                        {
                            LeafImageId = leafImage.Id,
                            SymptomId = symptom.Id,
                            ObservedByUserId = user.Id,
                            Notes = request.Notes
                        });
                    }
                    await _context.SaveChangesAsync();
                }

                // Gọi API dự đoán (mock implementation)
                var predictionResult = await PredictDiseaseAsync(leafImage);

                // Cập nhật trạng thái ảnh
                leafImage.ImageStatus = "Processed";
                await _context.SaveChangesAsync();

                return Ok(predictionResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi upload và phân tích ảnh");
                return StatusCode(500, "Có lỗi xảy ra khi xử lý ảnh");
            }
        }

        /// <summary>
        /// Lấy lịch sử dự đoán của người dùng
        /// </summary>
        /// <param name="pageNumber">Số trang</param>
        /// <param name="pageSize">Số bản ghi mỗi trang</param>
        /// <returns>Danh sách lịch sử dự đoán</returns>
        [HttpGet("history")]
        public async Task<ActionResult<List<PredictionHistory>>> GetPredictionHistory(
            int pageNumber = 1, int pageSize = 10)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            var predictions = await _context.Predictions
                .Include(p => p.LeafImage)
                .Include(p => p.Feedbacks)
                .Where(p => p.LeafImage.UserId == user.Id)
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

            return Ok(predictions);
        }

        /// <summary>
        /// Thêm phản hồi cho dự đoán
        /// </summary>
        /// <param name="request">Thông tin phản hồi</param>
        /// <returns>Kết quả phản hồi</returns>
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

                // Nếu rating thấp, thêm vào training data
                if (request.Rating <= 2 && !string.IsNullOrEmpty(request.CorrectDiseaseName))
                {
                    var trainingData = new TrainingData
                    {
                        LeafImageId = prediction.LeafImageId,
                        Label = request.CorrectDiseaseName,
                        Source = "Feedback",
                        FeedbackId = feedback.Id,
                        OriginalPrediction = prediction.DiseaseName,
                        OriginalConfidence = prediction.Confidence
                    };

                    _context.TrainingDataRecords.Add(trainingData);
                    await _context.SaveChangesAsync();
                }

                var response = new FeedbackResponse
                {
                    Id = feedback.Id,
                    Rating = feedback.Rating,
                    FeedbackText = feedback.FeedbackText,
                    CorrectDiseaseName = feedback.CorrectDiseaseName,
                    FeedbackDate = feedback.FeedbackDate,
                    IsUsedForTraining = false
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
        /// <returns>Danh sách triệu chứng</returns>
        [HttpGet("symptoms")]
        public async Task<ActionResult<List<SymptomDto>>> GetSymptoms()
        {
            var symptoms = await _context.Symptoms
                .Where(s => s.IsActive)
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

        #region Private Methods

        private async Task<string> CalculateImageHashAsync(string filePath)
        {
            using var md5 = MD5.Create();
            using var stream = System.IO.File.OpenRead(filePath);
            var hash = await md5.ComputeHashAsync(stream);
            return Convert.ToHexString(hash);
        }

        private async Task<PredictionResult> PredictDiseaseAsync(LeafImage leafImage)
        {
            // Mock implementation - thay thế bằng gọi model thực tế
            var random = new Random();
            var diseases = new[] { "Cercospora", "Healthy", "Miner", "Phoma", "Rust" };
            var selectedDisease = diseases[random.Next(diseases.Length)];
            var confidence = (decimal)(random.NextDouble() * 0.4 + 0.6); // 0.6 - 1.0

            var startTime = DateTime.UtcNow;

            // Simulate processing time
            await Task.Delay(1000);

            var processingTime = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;

            var prediction = new Prediction
            {
                LeafImageId = leafImage.Id,
                DiseaseName = selectedDisease,
                Confidence = confidence,
                ModelVersion = "v1.1",
                ProcessingTimeMs = processingTime,
                SeverityLevel = confidence > 0.8m ? "Nặng" : confidence > 0.6m ? "Trung bình" : "Nhẹ",
                TreatmentSuggestion = GetTreatmentSuggestion(selectedDisease)
            };

            _context.Predictions.Add(prediction);

            // Log prediction
            var predictionLog = new PredictionLog
            {
                LeafImageId = leafImage.Id,
                ModelType = "CNN",
                ApiStatus = "Success",
                ModelVersion = "v1.1",
                ProcessingTimeMs = processingTime,
                RequestId = Guid.NewGuid().ToString()
            };

            _context.PredictionLogs.Add(predictionLog);
            await _context.SaveChangesAsync();

            return new PredictionResult
            {
                Id = prediction.Id,
                DiseaseName = prediction.DiseaseName,
                Confidence = prediction.Confidence,
                ModelVersion = prediction.ModelVersion,
                SeverityLevel = prediction.SeverityLevel,
                TreatmentSuggestion = prediction.TreatmentSuggestion,
                PredictionDate = prediction.PredictionDate,
                ProcessingTimeMs = prediction.ProcessingTimeMs
            };
        }

        private string GetTreatmentSuggestion(string diseaseName)
        {
            return diseaseName switch
            {
                "Cercospora" => "Sử dụng thuốc diệt nấm chứa copper oxychloride. Cải thiện thoát nước và thông gió.",
                "Rust" => "Áp dụng thuốc diệt nấm hệ thống. Loại bỏ lá bị nhiễm. Tăng cường dinh dưỡng cho cây.",
                "Miner" => "Sử dụng thuốc trừ sâu sinh học. Loại bỏ lá bị tổn thương. Kiểm soát độ ẩm.",
                "Phoma" => "Sử dụng thuốc diệt nấm. Cải thiện dẫn lưu nước. Tránh tưới nước lên lá.",
                "Healthy" => "Cây khỏe mạnh. Tiếp tục chế độ chăm sóc hiện tại và theo dõi định kỳ.",
                _ => "Tham khảo ý kiến chuyên gia để có phương án điều trị phù hợp."
            };
        }

        #endregion
    }
}