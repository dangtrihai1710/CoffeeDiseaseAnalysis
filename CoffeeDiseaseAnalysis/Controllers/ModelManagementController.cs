// File: CoffeeDiseaseAnalysis/Controllers/ModelManagementController.cs - FIXED CS0119 Errors
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
    [Authorize(Roles = "Admin,Expert")]
    public class ModelManagementController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IPredictionService _predictionService;
        private readonly IMLPService _mlpService;
        private readonly ICacheService _cacheService;
        private readonly ILogger<ModelManagementController> _logger;
        private readonly IWebHostEnvironment _env;

        public ModelManagementController(
            ApplicationDbContext context,
            UserManager<User> userManager,
            IPredictionService predictionService,
            IMLPService mlpService,
            ICacheService cacheService,
            ILogger<ModelManagementController> logger,
            IWebHostEnvironment env)
        {
            _context = context;
            _userManager = userManager;
            _predictionService = predictionService;
            _mlpService = mlpService;
            _cacheService = cacheService;
            _logger = logger;
            _env = env;
        }

        /// <summary>
        /// Lấy danh sách tất cả model versions
        /// </summary>
        [HttpGet("versions")]
        public async Task<ActionResult<object>> GetModelVersions(
            int pageNumber = 1, int pageSize = 10, string? modelType = null)
        {
            var query = _context.ModelVersions.AsQueryable();

            if (!string.IsNullOrEmpty(modelType))
            {
                query = query.Where(m => m.ModelType == modelType);
            }

            var totalCount = await query.CountAsync();

            var models = await query
                .OrderByDescending(m => m.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(m => new
                {
                    m.Id,
                    m.ModelName,
                    m.Version,
                    m.ModelType,
                    m.Accuracy,
                    m.ValidationAccuracy,
                    m.TestAccuracy,
                    m.IsActive,
                    m.IsProduction,
                    m.CreatedAt,
                    m.DeployedAt,
                    m.TrainingDatasetVersion,
                    m.TrainingSamples,
                    m.FileSizeBytes,
                    m.Notes,
                    CreatedBy = m.CreatedByUser != null ? m.CreatedByUser.FullName : "System"
                })
                .ToListAsync();

            return Ok(new
            {
                Data = models,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        }

        /// <summary>
        /// Lấy chi tiết một model version
        /// </summary>
        [HttpGet("versions/{modelId}")]
        public async Task<ActionResult<object>> GetModelVersion(int modelId)
        {
            var model = await _context.ModelVersions
                .Include(m => m.CreatedByUser)
                .FirstOrDefaultAsync(m => m.Id == modelId);

            if (model == null)
            {
                return NotFound("Không tìm thấy model version");
            }

            // Tính thống kê từ predictions
            var predictionStats = await _context.Predictions
                .Where(p => p.ModelVersion == model.Version)
                .GroupBy(p => p.DiseaseName)
                .Select(g => new
                {
                    Disease = g.Key,
                    Count = g.Count(),
                    AvgConfidence = g.Average(p => (double)p.Confidence)
                })
                .ToListAsync();

            var feedbackStats = await _context.Feedbacks
                .Include(f => f.Prediction)
                .Where(f => f.Prediction.ModelVersion == model.Version)
                .GroupBy(f => f.Rating)
                .Select(g => new
                {
                    Rating = g.Key,
                    Count = g.Count()
                })
                .ToListAsync();

            var result = new
            {
                model.Id,
                model.ModelName,
                model.Version,
                model.ModelType,
                model.FilePath,
                model.Accuracy,
                model.ValidationAccuracy,
                model.TestAccuracy,
                model.IsActive,
                model.IsProduction,
                model.CreatedAt,
                model.DeployedAt,
                model.TrainingDatasetVersion,
                model.TrainingSamples,
                model.ValidationSamples,
                model.TestSamples,
                model.FileSizeBytes,
                model.Notes,
                model.FileChecksum,
                CreatedBy = model.CreatedByUser?.FullName ?? "System",
                Statistics = new
                {
                    TotalPredictions = predictionStats.Sum(s => s.Count),
                    PredictionsByDisease = predictionStats,
                    FeedbackDistribution = feedbackStats,
                    AvgRating = feedbackStats.Any()
                        ? feedbackStats.Sum(f => f.Rating * f.Count) / (double)feedbackStats.Sum(f => f.Count)
                        : 0
                }
            };

            return Ok(result);
        }

        /// <summary>
        /// Upload model mới
        /// </summary>
        [HttpPost("upload")]
        public async Task<ActionResult<object>> UploadModel([FromForm] UploadModelRequest request)
        {
            try
            {
                if (request.ModelFile == null || request.ModelFile.Length == 0)
                {
                    return BadRequest("Không có file model được upload");
                }

                // Validate file extension
                var allowedExtensions = new[] { ".onnx", ".h5", ".pkl", ".model" };
                var fileExtension = Path.GetExtension(request.ModelFile.FileName).ToLowerInvariant();

                if (!allowedExtensions.Contains(fileExtension))
                {
                    return BadRequest($"Chỉ hỗ trợ các định dạng: {string.Join(", ", allowedExtensions)}");
                }

                // Validate file size (max 500MB)
                if (request.ModelFile.Length > 500 * 1024 * 1024)
                {
                    return BadRequest("Kích thước file không được vượt quá 500MB");
                }

                var user = await _userManager.GetUserAsync(User);

                // Tạo thư mục models nếu chưa tồn tại
                var modelsDir = Path.Combine(_env.WebRootPath, "models");
                if (!Directory.Exists(modelsDir))
                {
                    Directory.CreateDirectory(modelsDir);
                }

                // Tạo tên file unique
                var fileName = $"{request.ModelName}_{request.Version}{fileExtension}";
                var filePath = Path.Combine(modelsDir, fileName);

                // Lưu file
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await request.ModelFile.CopyToAsync(stream);
                }

                // Tính checksum
                var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
                var checksum = CalculateFileChecksum(fileBytes);

                // Tạo bản ghi ModelVersion
                var modelVersion = new ModelVersion
                {
                    ModelName = request.ModelName,
                    Version = request.Version,
                    FilePath = $"/models/{fileName}",
                    Accuracy = request.Accuracy,
                    ValidationAccuracy = request.ValidationAccuracy,
                    TestAccuracy = request.TestAccuracy,
                    TrainingDatasetVersion = request.TrainingDatasetVersion ?? "unknown",
                    TrainingSamples = request.TrainingSamples,
                    ValidationSamples = request.ValidationSamples,
                    TestSamples = request.TestSamples,
                    ModelType = request.ModelType,
                    FileSizeBytes = request.ModelFile.Length,
                    Notes = request.Notes,
                    FileChecksum = checksum,
                    CreatedByUserId = user?.Id,
                    IsActive = false, // Mặc định không active
                    IsProduction = false
                };

                _context.ModelVersions.Add(modelVersion);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Model mới được upload: {ModelName} v{Version} by {User}",
                    request.ModelName, request.Version, user?.FullName ?? "Unknown");

                return Ok(new
                {
                    modelVersion.Id,
                    modelVersion.ModelName,
                    modelVersion.Version,
                    modelVersion.FilePath,
                    modelVersion.Accuracy,
                    modelVersion.CreatedAt,
                    Message = "Model đã được upload thành công. Sử dụng endpoint /activate để kích hoạt."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi upload model");
                return StatusCode(500, "Có lỗi xảy ra khi upload model");
            }
        }

        /// <summary>
        /// Kích hoạt model version
        /// </summary>
        [HttpPost("activate/{modelId}")]
        public async Task<ActionResult<object>> ActivateModel(int modelId)
        {
            try
            {
                var model = await _context.ModelVersions.FindAsync(modelId);
                if (model == null)
                {
                    return NotFound("Không tìm thấy model version");
                }

                // Kiểm tra file tồn tại
                var modelPath = Path.Combine(_env.WebRootPath, model.FilePath.TrimStart('/'));
                if (!System.IO.File.Exists(modelPath))
                {
                    return BadRequest("File model không tồn tại trên server");
                }

                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    // Deactivate models cùng type
                    var samTypeModels = await _context.ModelVersions
                        .Where(m => m.ModelType == model.ModelType && m.IsActive)
                        .ToListAsync();

                    foreach (var oldModel in samTypeModels)
                    {
                        oldModel.IsActive = false;
                        oldModel.IsProduction = false;
                    }

                    // Activate model mới
                    model.IsActive = true;
                    model.DeployedAt = DateTime.UtcNow;

                    await _context.SaveChangesAsync();

                    // Nếu là CNN model, switch trong prediction service
                    if (model.ModelType == "CNN" || model.ModelType == "Combined")
                    {
                        var switchResult = await _predictionService.SwitchModelVersionAsync(model.Version);
                        if (!switchResult)
                        {
                            await transaction.RollbackAsync();
                            return BadRequest("Không thể load model mới vào memory");
                        }
                    }

                    await transaction.CommitAsync();

                    // Invalidate cache
                    await _cacheService.InvalidateModelCacheAsync(model.Version);

                    _logger.LogInformation("Model activated: {ModelName} v{Version}", model.ModelName, model.Version);

                    return Ok(new
                    {
                        model.Id,
                        model.ModelName,
                        model.Version,
                        model.IsActive,
                        model.DeployedAt,
                        Message = "Model đã được kích hoạt thành công"
                    });
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi kích hoạt model {ModelId}", modelId);
                return StatusCode(500, "Có lỗi xảy ra khi kích hoạt model");
            }
        }

        /// <summary>
        /// Đưa model vào production
        /// </summary>
        [HttpPost("deploy/{modelId}")]
        public async Task<ActionResult<object>> DeployToProduction(int modelId)
        {
            try
            {
                var model = await _context.ModelVersions.FindAsync(modelId);
                if (model == null)
                {
                    return NotFound("Không tìm thấy model version");
                }

                if (!model.IsActive)
                {
                    return BadRequest("Model phải được activate trước khi deploy production");
                }

                // Deactivate production model hiện tại cùng type
                var currentProdModel = await _context.ModelVersions
                    .FirstOrDefaultAsync(m => m.ModelType == model.ModelType && m.IsProduction);

                if (currentProdModel != null)
                {
                    currentProdModel.IsProduction = false;
                }

                // Deploy model mới
                model.IsProduction = true;
                model.DeployedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Model deployed to production: {ModelName} v{Version}",
                    model.ModelName, model.Version);

                return Ok(new
                {
                    model.Id,
                    model.ModelName,
                    model.Version,
                    model.IsProduction,
                    model.DeployedAt,
                    Message = "Model đã được deploy lên production thành công"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi deploy model {ModelId}", modelId);
                return StatusCode(500, "Có lỗi xảy ra khi deploy model");
            }
        }

        /// <summary>
        /// A/B Test - Chia traffic giữa 2 models
        /// </summary>
        [HttpPost("ab-test")]
        public async Task<ActionResult<object>> SetupABTest([FromBody] ABTestRequest request)
        {
            try
            {
                var modelA = await _context.ModelVersions.FindAsync(request.ModelAId);
                var modelB = await _context.ModelVersions.FindAsync(request.ModelBId);

                if (modelA == null || modelB == null)
                {
                    return BadRequest("Một hoặc cả hai model không tồn tại");
                }

                if (modelA.ModelType != modelB.ModelType)
                {
                    return BadRequest("Chỉ có thể A/B test giữa các model cùng type");
                }

                // TODO: Implement A/B testing logic
                // Có thể lưu thông tin A/B test vào bảng riêng
                // Và modify PredictionService để chọn model theo tỷ lệ

                _logger.LogInformation("A/B Test setup: {ModelA} vs {ModelB} with traffic {TrafficA}:{TrafficB}",
                    modelA.Version, modelB.Version, request.TrafficPercentageA, request.TrafficPercentageB);

                return Ok(new
                {
                    Message = "A/B Test đã được thiết lập",
                    ModelA = new { modelA.Id, modelA.Version, TrafficPercentage = request.TrafficPercentageA },
                    ModelB = new { modelB.Id, modelB.Version, TrafficPercentage = request.TrafficPercentageB },
                    Duration = request.TestDurationDays
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi setup A/B test");
                return StatusCode(500, "Có lỗi xảy ra khi setup A/B test");
            }
        }

        /// <summary>
        /// Huấn luyện lại MLP model từ feedback data
        /// </summary>
        [HttpPost("retrain-mlp")]
        public async Task<ActionResult<object>> RetrainMLPModel()
        {
            try
            {
                // Kiểm tra xem có đủ training data không
                var trainingDataCount = await _context.TrainingDataRecords
                    .Where(t => t.IsValidated && !t.IsUsedForTraining)
                    .CountAsync();

                if (trainingDataCount < 50)
                {
                    return BadRequest($"Không đủ dữ liệu để huấn luyện lại. Cần ít nhất 50 mẫu, hiện có {trainingDataCount}");
                }

                // Bắt đầu huấn luyện (async process)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _mlpService.TrainMLPModelAsync();
                        _logger.LogInformation("MLP model retrained successfully with {Count} samples", trainingDataCount);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to retrain MLP model");
                    }
                });

                return Ok(new
                {
                    Message = "Quá trình huấn luyện lại MLP model đã được bắt đầu",
                    TrainingDataCount = trainingDataCount,
                    EstimatedTime = "5-10 phút"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi bắt đầu huấn luyện lại MLP");
                return StatusCode(500, "Có lỗi xảy ra khi khởi động huấn luyện MLP");
            }
        }

        /// <summary>
        /// Xóa model version (chỉ những model không active)
        /// </summary>
        [HttpDelete("{modelId}")]
        public async Task<ActionResult> DeleteModel(int modelId)
        {
            try
            {
                var model = await _context.ModelVersions.FindAsync(modelId);
                if (model == null)
                {
                    return NotFound("Không tìm thấy model version");
                }

                if (model.IsActive || model.IsProduction)
                {
                    return BadRequest("Không thể xóa model đang active hoặc production");
                }

                // Xóa file
                var modelPath = Path.Combine(_env.WebRootPath, model.FilePath.TrimStart('/'));
                if (System.IO.File.Exists(modelPath))
                {
                    System.IO.File.Delete(modelPath);
                }

                _context.ModelVersions.Remove(model);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Model deleted: {ModelName} v{Version}", model.ModelName, model.Version);

                return Ok(new { Message = "Model đã được xóa thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xóa model {ModelId}", modelId);
                return StatusCode(500, "Có lỗi xảy ra khi xóa model");
            }
        }

        #region Private Methods

        private string CalculateFileChecksum(byte[] fileBytes)
        {
            using var md5 = MD5.Create();
            var hash = md5.ComputeHash(fileBytes);
            return Convert.ToHexString(hash);
        }

        #endregion
    }
}