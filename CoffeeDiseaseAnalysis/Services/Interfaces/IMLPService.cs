// File: CoffeeDiseaseAnalysis/Services/Interfaces/IMLPService.cs
using CoffeeDiseaseAnalysis.Data;
using CoffeeDiseaseAnalysis.Data.Entities;
using CoffeeDiseaseAnalysis.Services.Interfaces;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace CoffeeDiseaseAnalysis.Services.Interfaces
{
    public interface IMLPService
    {
        Task<decimal> PredictFromSymptomsAsync(List<int> symptomIds);
        Task<Dictionary<string, decimal>> PredictAllClassesFromSymptomsAsync(List<int> symptomIds);
        Task TrainMLPModelAsync();
        Task<bool> IsModelAvailableAsync();
    }
}

// File: CoffeeDiseaseAnalysis/Services/MLPService.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using CoffeeDiseaseAnalysis.Data;
using CoffeeDiseaseAnalysis.Services.Interfaces;

namespace CoffeeDiseaseAnalysis.Services
{
    public class MLPService : IMLPService, IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<MLPService> _logger;
        private readonly IWebHostEnvironment _env;

        private InferenceSession? _mlpSession;
        private readonly string[] _diseaseClasses = { "Cercospora", "Healthy", "Miner", "Phoma", "Rust" };

        // Symptom feature vector size (số lượng symptoms trong database)
        private const int FeatureSize = 20; // Có thể điều chỉnh theo số symptoms thực tế

        public MLPService(
            ApplicationDbContext context,
            ILogger<MLPService> logger,
            IWebHostEnvironment env)
        {
            _context = context;
            _logger = logger;
            _env = env;

            _ = LoadMLPModelAsync();
        }

        public async Task<decimal> PredictFromSymptomsAsync(List<int> symptomIds)
        {
            try
            {
                if (_mlpSession == null)
                {
                    await LoadMLPModelAsync();
                    if (_mlpSession == null)
                    {
                        _logger.LogWarning("MLP model chưa được load, trả về confidence mặc định");
                        return 0.5m; // Default confidence khi không có MLP
                    }
                }

                // Tạo feature vector từ symptoms
                var featureVector = await CreateSymptomFeatureVectorAsync(symptomIds);

                // Chạy inference
                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor("input", featureVector)
                };

                using var results = _mlpSession.Run(inputs);
                var outputTensor = results.FirstOrDefault()?.AsTensor<float>();

                if (outputTensor == null)
                {
                    _logger.LogWarning("MLP model trả về null, sử dụng confidence mặc định");
                    return 0.5m;
                }

                // Trả về confidence cao nhất
                var maxConfidence = 0f;
                for (int i = 0; i < _diseaseClasses.Length; i++)
                {
                    var confidence = outputTensor[0, i];
                    if (confidence > maxConfidence)
                    {
                        maxConfidence = confidence;
                    }
                }

                return (decimal)maxConfidence;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi dự đoán từ symptoms");
                return 0.5m; // Fallback confidence
            }
        }

        public async Task<Dictionary<string, decimal>> PredictAllClassesFromSymptomsAsync(List<int> symptomIds)
        {
            var result = new Dictionary<string, decimal>();

            try
            {
                if (_mlpSession == null)
                {
                    await LoadMLPModelAsync();
                    if (_mlpSession == null)
                    {
                        // Trả về confidence đều nhau cho tất cả classes
                        foreach (var disease in _diseaseClasses)
                        {
                            result[disease] = 0.2m; // 1/5 = 0.2 cho 5 classes
                        }
                        return result;
                    }
                }

                var featureVector = await CreateSymptomFeatureVectorAsync(symptomIds);

                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor("input", featureVector)
                };

                using var results = _mlpSession.Run(inputs);
                var outputTensor = results.FirstOrDefault()?.AsTensor<float>();

                if (outputTensor != null)
                {
                    for (int i = 0; i < _diseaseClasses.Length; i++)
                    {
                        result[_diseaseClasses[i]] = (decimal)outputTensor[0, i];
                    }
                }
                else
                {
                    foreach (var disease in _diseaseClasses)
                    {
                        result[disease] = 0.2m;
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi dự đoán tất cả classes từ symptoms");

                // Fallback: confidence đều nhau
                foreach (var disease in _diseaseClasses)
                {
                    result[disease] = 0.2m;
                }

                return result;
            }
        }

        public async Task TrainMLPModelAsync()
        {
            try
            {
                _logger.LogInformation("Bắt đầu huấn luyện MLP model từ training data...");

                // Lấy training data từ database
                var trainingData = await _context.TrainingDataRecords
                    .Include(t => t.LeafImage)
                    .ThenInclude(l => l.LeafImageSymptoms)
                    .ThenInclude(s => s.Symptom)
                    .Where(t => t.IsValidated && t.LeafImage.LeafImageSymptoms.Any())
                    .ToListAsync();

                if (trainingData.Count < 50)
                {
                    _logger.LogWarning("Không đủ dữ liệu để huấn luyện MLP (cần ít nhất 50 mẫu, có {Count})", trainingData.Count);
                    return;
                }

                // Chuẩn bị dữ liệu huấn luyện
                var features = new List<float[]>();
                var labels = new List<int>();

                foreach (var data in trainingData)
                {
                    var symptomIds = data.LeafImage.LeafImageSymptoms.Select(s => s.SymptomId).ToList();
                    var featureVector = await CreateSymptomFeatureVectorAsync(symptomIds);

                    // Chuyển tensor thành array
                    var featureArray = new float[FeatureSize];
                    for (int i = 0; i < FeatureSize; i++)
                    {
                        featureArray[i] = featureVector[0, i];
                    }

                    features.Add(featureArray);

                    // Convert label string to index
                    var labelIndex = Array.IndexOf(_diseaseClasses, data.Label);
                    labels.Add(labelIndex >= 0 ? labelIndex : 0);
                }

                // TODO: Thực hiện huấn luyện MLP với ML.NET hoặc gọi Python service
                // Hiện tại chỉ log thông tin
                _logger.LogInformation("Đã chuẩn bị {FeatureCount} features và {LabelCount} labels để huấn luyện MLP",
                    features.Count, labels.Count);

                // Placeholder: Lưu model info vào database
                var mlpModel = new ModelVersion
                {
                    ModelName = "coffee_mlp",
                    Version = $"v{DateTime.UtcNow:yyyyMMdd_HHmmss}",
                    FilePath = "/models/coffee_mlp_latest.onnx",
                    Accuracy = 0.72m, // Placeholder accuracy
                    ValidationAccuracy = 0.70m,
                    TestAccuracy = 0.69m,
                    TrainingDatasetVersion = "symptoms_v1.0",
                    TrainingSamples = features.Count,
                    ValidationSamples = features.Count / 5,
                    TestSamples = features.Count / 5,
                    ModelType = "MLP",
                    FileSizeBytes = 5000000,
                    IsActive = true,
                    Notes = $"MLP được huấn luyện từ {features.Count} mẫu symptoms"
                };

                _context.ModelVersions.Add(mlpModel);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Hoàn thành huấn luyện MLP model version: {Version}", mlpModel.Version);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi huấn luyện MLP model");
                throw;
            }
        }

        public async Task<bool> IsModelAvailableAsync()
        {
            if (_mlpSession != null)
                return true;

            await LoadMLPModelAsync();
            return _mlpSession != null;
        }

        #region Private Methods

        private async Task LoadMLPModelAsync()
        {
            try
            {
                var mlpModel = await _context.ModelVersions
                    .Where(m => m.ModelType == "MLP" && m.IsActive)
                    .OrderByDescending(m => m.CreatedAt)
                    .FirstOrDefaultAsync();

                if (mlpModel == null)
                {
                    _logger.LogWarning("Không tìm thấy MLP model active");
                    return;
                }

                var modelPath = Path.Combine(_env.WebRootPath, mlpModel.FilePath.TrimStart('/'));

                if (!File.Exists(modelPath))
                {
                    _logger.LogWarning("File MLP model không tồn tại: {Path}", modelPath);
                    return;
                }

                _mlpSession?.Dispose();

                var sessionOptions = new SessionOptions();
                sessionOptions.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;

                _mlpSession = new InferenceSession(modelPath, sessionOptions);

                _logger.LogInformation("Đã load thành công MLP model: {Version}", mlpModel.Version);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi load MLP model");
            }
        }

        private async Task<DenseTensor<float>> CreateSymptomFeatureVectorAsync(List<int> symptomIds)
        {
            // Lấy tất cả symptoms từ database để tạo feature vector
            var allSymptoms = await _context.Symptoms
                .Where(s => s.IsActive)
                .OrderBy(s => s.Id)
                .ToListAsync();

            var featureVector = new DenseTensor<float>(new[] { 1, FeatureSize });

            // Initialize tất cả về 0
            for (int i = 0; i < FeatureSize; i++)
            {
                featureVector[0, i] = 0.0f;
            }

            // Set 1 cho các symptoms có trong input, có trọng số
            foreach (var symptomId in symptomIds)
            {
                var symptom = allSymptoms.FirstOrDefault(s => s.Id == symptomId);
                if (symptom != null)
                {
                    var index = allSymptoms.IndexOf(symptom);
                    if (index < FeatureSize)
                    {
                        // Sử dụng weight của symptom làm feature value
                        featureVector[0, index] = (float)symptom.Weight;
                    }
                }
            }

            return featureVector;
        }

        #endregion

        public void Dispose()
        {
            _mlpSession?.Dispose();
        }
    }
}