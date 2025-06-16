// File: CoffeeDiseaseAnalysis/Services/PredictionService.cs - FINAL FIX
using CoffeeDiseaseAnalysis.Data;
using CoffeeDiseaseAnalysis.Data.Entities;
using CoffeeDiseaseAnalysis.Models.DTOs;
using CoffeeDiseaseAnalysis.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Diagnostics;

namespace CoffeeDiseaseAnalysis.Services
{
    public class PredictionService : IPredictionService, IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly ICacheService _cacheService;
        private readonly IMLPService _mlpService;
        private readonly ILogger<PredictionService> _logger;
        private readonly IWebHostEnvironment _env;

        private InferenceSession? _currentSession;
        private ModelVersion? _currentModel;
        private readonly string[] _diseaseClasses = { "Cercospora", "Healthy", "Miner", "Phoma", "Rust" };

        // Model input parameters
        private const int ImageSize = 224;
        private const int ChannelCount = 3;

        public PredictionService(
            ApplicationDbContext context,
            ICacheService cacheService,
            IMLPService mlpService,
            ILogger<PredictionService> logger,
            IWebHostEnvironment env)
        {
            _context = context;
            _cacheService = cacheService;
            _mlpService = mlpService;
            _logger = logger;
            _env = env;

            // Load active model khi service khởi tạo (async)
            _ = Task.Run(LoadActiveModelAsync);
        }

        public async Task<PredictionResult> PredictDiseaseAsync(byte[] imageBytes, string imagePath, List<int>? symptomIds = null)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Kiểm tra cache trước
                var imageHash = CalculateImageHash(imageBytes);
                var cachedResult = await _cacheService.GetPredictionAsync(imageHash);
                if (cachedResult != null)
                {
                    _logger.LogInformation("Cache hit for image hash: {Hash}", imageHash);
                    return cachedResult;
                }

                // Đảm bảo model đã được load
                if (_currentSession == null || _currentModel == null)
                {
                    await LoadActiveModelAsync();
                    if (_currentSession == null)
                    {
                        throw new InvalidOperationException("Không thể load model AI");
                    }
                }

                // Tiền xử lý ảnh
                var preprocessedImage = PreprocessImage(imageBytes);

                // Chạy inference
                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor("input", preprocessedImage)
                };

                using var results = _currentSession.Run(inputs);
                var outputTensor = results.FirstOrDefault()?.AsTensor<float>();

                if (outputTensor == null)
                {
                    throw new InvalidOperationException("Model trả về kết quả null");
                }

                // Parse kết quả
                var predictions = ParseModelOutput(outputTensor);
                var topPrediction = predictions.OrderByDescending(p => p.Confidence).First();

                // Kết hợp với MLP nếu có symptoms
                decimal? finalConfidence = topPrediction.Confidence;
                if (symptomIds?.Any() == true)
                {
                    var mlpResult = await _mlpService.PredictFromSymptomsAsync(symptomIds);
                    finalConfidence = CombineCnnMlpResults(topPrediction.Confidence, mlpResult);
                }

                var result = new PredictionResult
                {
                    DiseaseName = topPrediction.DiseaseName,
                    Confidence = topPrediction.Confidence,
                    FinalConfidence = finalConfidence,
                    ModelVersion = _currentModel.Version,
                    SeverityLevel = DetermineSeverityLevel(finalConfidence ?? topPrediction.Confidence),
                    TreatmentSuggestion = GetTreatmentSuggestion(topPrediction.DiseaseName),
                    PredictionDate = DateTime.UtcNow,
                    ProcessingTimeMs = (int)stopwatch.ElapsedMilliseconds
                };

                // Cache kết quả
                await _cacheService.SetPredictionAsync(imageHash, result, TimeSpan.FromDays(7));

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi dự đoán bệnh cho ảnh: {ImagePath}", imagePath);
                throw;
            }
            finally
            {
                stopwatch.Stop();
            }
        }

        public async Task<BatchPredictionResponse> PredictBatchAsync(List<byte[]> imagesBytes, List<string> imagePaths)
        {
            var response = new BatchPredictionResponse();
            var totalStartTime = Stopwatch.StartNew();

            for (int i = 0; i < imagesBytes.Count; i++)
            {
                try
                {
                    var result = await PredictDiseaseAsync(imagesBytes[i], imagePaths[i]);
                    response.Results.Add(result);
                    response.SuccessCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi xử lý ảnh batch: {ImagePath}", imagePaths[i]);
                    response.Errors.Add($"Ảnh {imagePaths[i]}: {ex.Message}");
                    response.FailureCount++;
                }
            }

            response.TotalProcessed = imagesBytes.Count;
            response.TotalProcessingTimeMs = (int)totalStartTime.ElapsedMilliseconds;

            return response;
        }

        public async Task<ModelStatistics> GetCurrentModelInfoAsync()
        {
            var model = await _context.ModelVersions
                .Where(m => m.IsActive && m.IsProduction)
                .FirstOrDefaultAsync();

            if (model == null)
            {
                throw new InvalidOperationException("Không tìm thấy model đang hoạt động");
            }

            // Tính thống kê từ database
            var totalPredictions = await _context.Predictions
                .Where(p => p.ModelVersion == model.Version)
                .CountAsync();

            var avgConfidence = await _context.Predictions
                .Where(p => p.ModelVersion == model.Version)
                .AverageAsync(p => (double?)p.Confidence) ?? 0;

            var avgRating = await _context.Feedbacks
                .Include(f => f.Prediction)
                .Where(f => f.Prediction.ModelVersion == model.Version)
                .AverageAsync(f => (double?)f.Rating) ?? 0;

            return new ModelStatistics
            {
                ModelName = model.ModelName,
                Version = model.Version,
                Accuracy = model.Accuracy,
                ValidationAccuracy = model.ValidationAccuracy,
                TestAccuracy = model.TestAccuracy,
                IsActive = model.IsActive,
                IsProduction = model.IsProduction,
                TotalPredictions = totalPredictions,
                AverageConfidence = (decimal)avgConfidence,
                AverageRating = (decimal)avgRating,
                CreatedAt = model.CreatedAt,
                DeployedAt = model.DeployedAt
            };
        }

        public async Task<bool> SwitchModelVersionAsync(string modelVersion)
        {
            try
            {
                var newModel = await _context.ModelVersions
                    .FirstOrDefaultAsync(m => m.Version == modelVersion && m.IsActive);

                if (newModel == null)
                {
                    _logger.LogWarning("Không tìm thấy model version: {Version}", modelVersion);
                    return false;
                }

                // Deactivate current production model
                var currentModel = await _context.ModelVersions
                    .FirstOrDefaultAsync(m => m.IsProduction);

                if (currentModel != null)
                {
                    currentModel.IsProduction = false;
                }

                // Activate new model
                newModel.IsProduction = true;
                newModel.DeployedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // Reload model in memory
                await LoadActiveModelAsync();

                _logger.LogInformation("Chuyển đổi thành công sang model version: {Version}", modelVersion);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi chuyển đổi model version: {Version}", modelVersion);
                return false;
            }
        }

        public async Task<bool> HealthCheckAsync()
        {
            try
            {
                if (_currentSession == null)
                {
                    await LoadActiveModelAsync();
                }

                if (_currentSession == null)
                    return false;

                // Test với ảnh dummy
                var testImage = CreateDummyImage();
                var preprocessed = PreprocessImage(testImage);

                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor("input", preprocessed)
                };

                using var results = _currentSession.Run(inputs);
                return results?.Any() == true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check failed");
                return false;
            }
        }

        #region Private Methods

        private async Task LoadActiveModelAsync()
        {
            try
            {
                var activeModel = await _context.ModelVersions
                    .Where(m => m.IsActive && m.IsProduction)
                    .OrderByDescending(m => m.CreatedAt)
                    .FirstOrDefaultAsync();

                if (activeModel == null)
                {
                    _logger.LogWarning("Không tìm thấy model active nào");
                    return;
                }

                var modelPath = Path.Combine(_env.WebRootPath, activeModel.FilePath.TrimStart('/'));

                if (!File.Exists(modelPath))
                {
                    _logger.LogError("File model không tồn tại: {Path}", modelPath);
                    return;
                }

                // Dispose session cũ nếu có
                _currentSession?.Dispose();

                // Load model mới với session options tối ưu - Fix ambiguity
                var sessionOptions = new Microsoft.ML.OnnxRuntime.SessionOptions();
                sessionOptions.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;

                // Tối ưu cho CPU
                sessionOptions.ExecutionMode = ExecutionMode.ORT_SEQUENTIAL;
                sessionOptions.EnableCpuMemArena = true;
                sessionOptions.EnableMemoryPattern = true;

                _currentSession = new InferenceSession(modelPath, sessionOptions);
                _currentModel = activeModel;

                _logger.LogInformation("Đã load thành công model: {ModelName} v{Version}",
                    activeModel.ModelName, activeModel.Version);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi load model");
                throw;
            }
        }

        private DenseTensor<float> PreprocessImage(byte[] imageBytes)
        {
            using var image = Image.Load<Rgb24>(imageBytes);

            // Resize về 224x224
            image.Mutate(x => x.Resize(ImageSize, ImageSize));

            // Chuyển về tensor với shape [1, 3, 224, 224] - CHW format
            var tensor = new DenseTensor<float>(new[] { 1, ChannelCount, ImageSize, ImageSize });

            // Normalize về [0,1] và chuyển RGB -> CHW
            for (int y = 0; y < ImageSize; y++)
            {
                for (int x = 0; x < ImageSize; x++)
                {
                    var pixel = image[x, y];
                    // ImageNet normalization
                    tensor[0, 0, y, x] = (pixel.R / 255.0f - 0.485f) / 0.229f; // Red channel
                    tensor[0, 1, y, x] = (pixel.G / 255.0f - 0.456f) / 0.224f; // Green channel  
                    tensor[0, 2, y, x] = (pixel.B / 255.0f - 0.406f) / 0.225f; // Blue channel
                }
            }

            return tensor;
        }

        private List<(string DiseaseName, decimal Confidence)> ParseModelOutput(Tensor<float> outputTensor)
        {
            var results = new List<(string DiseaseName, decimal Confidence)>();

            // Giả sử output có shape [1, 5] với 5 classes
            // Apply softmax để normalize
            var logits = new float[_diseaseClasses.Length];
            for (int i = 0; i < _diseaseClasses.Length; i++)
            {
                logits[i] = outputTensor[0, i];
            }

            // Softmax
            var maxLogit = logits.Max();
            var expValues = logits.Select(x => Math.Exp(x - maxLogit)).ToArray();
            var sumExp = expValues.Sum();

            for (int i = 0; i < _diseaseClasses.Length; i++)
            {
                var confidence = (decimal)(expValues[i] / sumExp);
                results.Add((_diseaseClasses[i], confidence));
            }

            return results;
        }

        private decimal CombineCnnMlpResults(decimal cnnConfidence, decimal mlpConfidence)
        {
            // Kết hợp với trọng số 70% CNN, 30% MLP
            return 0.7m * cnnConfidence + 0.3m * mlpConfidence;
        }

        private string DetermineSeverityLevel(decimal confidence)
        {
            return confidence switch
            {
                >= 0.8m => "Nặng",
                >= 0.6m => "Trung bình",
                >= 0.4m => "Nhẹ",
                _ => "Không rõ"
            };
        }

        private string GetTreatmentSuggestion(string diseaseName)
        {
            return diseaseName switch
            {
                "Cercospora" => "Sử dụng thuốc diệt nấm chứa copper oxychloride. Cải thiện thoát nước và thông gió. Loại bỏ lá bị nhiễm.",
                "Rust" => "Áp dụng thuốc diệt nấm hệ thống như propiconazole. Loại bỏ lá bị nhiễm. Tăng cường dinh dưỡng cho cây.",
                "Miner" => "Sử dụng thuốc trừ sâu sinh học như Beauveria bassiana. Loại bỏ lá bị tổn thương. Kiểm soát độ ẩm.",
                "Phoma" => "Sử dụng thuốc diệt nấm chứa azoxystrobin. Cải thiện dẫn lưu nước. Tránh tưới nước lên lá.",
                "Healthy" => "Cây khỏe mạnh. Tiếp tục chế độ chăm sóc hiện tại và theo dõi định kỳ để phát hiện sớm bệnh.",
                _ => "Tham khảo ý kiến chuyên gia nông nghiệp để có phương án điều trị phù hợp với tình trạng cụ thể."
            };
        }

        private string CalculateImageHash(byte[] imageBytes)
        {
            using var md5 = System.Security.Cryptography.MD5.Create();
            var hash = md5.ComputeHash(imageBytes);
            return Convert.ToHexString(hash);
        }

        private byte[] CreateDummyImage()
        {
            // Tạo ảnh dummy 224x224 RGB cho health check
            using var image = new Image<Rgb24>(ImageSize, ImageSize);

            // Fill với pattern đơn giản
            for (int y = 0; y < ImageSize; y++)
            {
                for (int x = 0; x < ImageSize; x++)
                {
                    var color = new Rgb24(
                        (byte)((x + y) % 256),
                        (byte)(x % 256),
                        (byte)(y % 256)
                    );
                    image[x, y] = color;
                }
            }

            using var ms = new MemoryStream();
            image.SaveAsJpeg(ms);
            return ms.ToArray();
        }

        #endregion

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _currentSession?.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}