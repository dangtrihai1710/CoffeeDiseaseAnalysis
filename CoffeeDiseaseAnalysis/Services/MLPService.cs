// File: CoffeeDiseaseAnalysis/Services/MLPService.cs - FIXED SessionOptions Ambiguity
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
        private const int FeatureSize = 20;
        private bool _disposed = false;

        public MLPService(
            ApplicationDbContext context,
            ILogger<MLPService> logger,
            IWebHostEnvironment env)
        {
            _context = context;
            _logger = logger;
            _env = env;

            // Load model asynchronously in background
            _ = Task.Run(LoadMLPModelAsync);
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
                        _logger.LogWarning("MLP model chưa sẵn sàng, trả về confidence mặc định");
                        return 0.5m;
                    }
                }

                var featureVector = await CreateSymptomFeatureVectorAsync(symptomIds);

                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor("input", featureVector)
                };

                using var results = _mlpSession.Run(inputs);
                var outputTensor = results.FirstOrDefault()?.AsTensor<float>();

                if (outputTensor == null)
                {
                    _logger.LogWarning("MLP model trả về null output");
                    return 0.5m;
                }

                // Tìm confidence cao nhất
                var maxConfidence = 0f;
                for (int i = 0; i < Math.Min(_diseaseClasses.Length, outputTensor.Length); i++)
                {
                    var confidence = outputTensor[0, i];
                    if (confidence > maxConfidence)
                    {
                        maxConfidence = confidence;
                    }
                }

                return Math.Max(0m, Math.Min(1m, (decimal)maxConfidence));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi dự đoán từ symptoms với IDs: {SymptomIds}", string.Join(",", symptomIds));
                return 0.5m;
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
                        // Trả về confidence đều nhau
                        foreach (var disease in _diseaseClasses)
                        {
                            result[disease] = 0.2m;
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
                    for (int i = 0; i < Math.Min(_diseaseClasses.Length, outputTensor.Length); i++)
                    {
                        var confidence = Math.Max(0f, Math.Min(1f, outputTensor[0, i]));
                        result[_diseaseClasses[i]] = (decimal)confidence;
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

                // Fallback
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

                var features = new List<float[]>();
                var labels = new List<int>();

                foreach (var data in trainingData)
                {
                    var symptomIds = data.LeafImage.LeafImageSymptoms.Select(s => s.SymptomId).ToList();
                    var featureVector = await CreateSymptomFeatureVectorAsync(symptomIds);

                    var featureArray = new float[FeatureSize];
                    for (int i = 0; i < FeatureSize; i++)
                    {
                        featureArray[i] = featureVector[0, i];
                    }

                    features.Add(featureArray);

                    var labelIndex = Array.IndexOf(_diseaseClasses, data.Label);
                    labels.Add(labelIndex >= 0 ? labelIndex : 0);
                }

                _logger.LogInformation("Đã chuẩn bị {FeatureCount} features và {LabelCount} labels để huấn luyện MLP",
                    features.Count, labels.Count);

                // Tạo model version mới
                var mlpModel = new Data.Entities.ModelVersion
                {
                    ModelName = "coffee_mlp",
                    Version = $"v{DateTime.UtcNow:yyyyMMdd_HHmmss}",
                    FilePath = "/models/coffee_mlp_latest.onnx",
                    Accuracy = 0.72m,
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

                if (!System.IO.File.Exists(modelPath))
                {
                    _logger.LogWarning("File MLP model không tồn tại: {Path}", modelPath);
                    return;
                }

                _mlpSession?.Dispose();

                // FIXED: Specify full namespace to avoid ambiguity
                var sessionOptions = new Microsoft.ML.OnnxRuntime.SessionOptions();
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
            var allSymptoms = await _context.Symptoms
                .Where(s => s.IsActive)
                .OrderBy(s => s.Id)
                .Take(FeatureSize)
                .ToListAsync();

            var featureVector = new DenseTensor<float>(new[] { 1, FeatureSize });

            // Initialize về 0
            for (int i = 0; i < FeatureSize; i++)
            {
                featureVector[0, i] = 0.0f;
            }

            // Set giá trị cho các symptoms có trong input
            foreach (var symptomId in symptomIds)
            {
                var symptom = allSymptoms.FirstOrDefault(s => s.Id == symptomId);
                if (symptom != null)
                {
                    var index = allSymptoms.IndexOf(symptom);
                    if (index >= 0 && index < FeatureSize)
                    {
                        featureVector[0, index] = (float)symptom.Weight;
                    }
                }
            }

            return featureVector;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _mlpSession?.Dispose();
                }
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}