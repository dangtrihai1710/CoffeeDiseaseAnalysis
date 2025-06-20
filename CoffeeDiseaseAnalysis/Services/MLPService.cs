// File: CoffeeDiseaseAnalysis/Services/MLPService.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using CoffeeDiseaseAnalysis.Data;
using CoffeeDiseaseAnalysis.Services.Interfaces;
using CoffeeDiseaseAnalysis.Models.DTOs;

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

        public async Task<MLPPredictionResult> PredictFromSymptomsDetailedAsync(List<int> symptomIds)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                var allClassProbabilities = await PredictAllClassesFromSymptomsAsync(symptomIds);
                var topPrediction = allClassProbabilities.OrderByDescending(x => x.Value).First();

                return new MLPPredictionResult
                {
                    DiseaseName = topPrediction.Key,
                    Confidence = topPrediction.Value,
                    AllClassProbabilities = allClassProbabilities,
                    PredictionDate = DateTime.UtcNow,
                    ProcessingTimeMs = (int)stopwatch.ElapsedMilliseconds,
                    ModelVersion = "MLP_v1.0",
                    TotalSymptoms = symptomIds?.Count ?? 0,
                    Features = symptomIds?.Select(id => $"Symptom_{id}").ToList() ?? new(),
                    IsReliable = (symptomIds?.Count ?? 0) >= 3 && _mlpSession != null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in MLP detailed prediction");

                // Return fallback result
                return new MLPPredictionResult
                {
                    DiseaseName = "Healthy",
                    Confidence = 0.5m,
                    AllClassProbabilities = _diseaseClasses.ToDictionary(d => d, d => 0.2m),
                    PredictionDate = DateTime.UtcNow,
                    ProcessingTimeMs = (int)stopwatch.ElapsedMilliseconds,
                    ModelVersion = "MLP_v1.0_FALLBACK",
                    TotalSymptoms = symptomIds?.Count ?? 0,
                    Features = new List<string>(),
                    IsReliable = false
                };
            }
            finally
            {
                stopwatch.Stop();
            }
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
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PredictAllClassesFromSymptomsAsync");
                foreach (var disease in _diseaseClasses)
                {
                    result[disease] = 0.2m;
                }
            }

            return result;
        }

        public async Task TrainMLPModelAsync()
        {
            await Task.Delay(1000);
            _logger.LogInformation("MLP model training simulation completed");
        }

        public async Task<bool> IsModelAvailableAsync()
        {
            await Task.Delay(50);
            return _mlpSession != null;
        }

        private async Task LoadMLPModelAsync()
        {
            try
            {
                _logger.LogInformation("🔄 Loading MLP model...");

                var possibleModelPaths = new[]
                {
                    Path.Combine(_env.WebRootPath ?? "wwwroot", "models", "coffee_mlp_v1.0.onnx"),
                    Path.Combine(_env.ContentRootPath, "wwwroot", "models", "coffee_mlp_v1.0.onnx"),
                    Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "models", "coffee_mlp_v1.0.onnx")
                };

                string? modelPath = null;
                foreach (var path in possibleModelPaths)
                {
                    if (File.Exists(path))
                    {
                        modelPath = path;
                        break;
                    }
                }

                if (string.IsNullOrEmpty(modelPath))
                {
                    _logger.LogWarning("⚠️ MLP model file not found, service will use fallback predictions");
                    return;
                }

                var sessionOptions = new Microsoft.ML.OnnxRuntime.SessionOptions() // FULLY QUALIFIED TO AVOID AMBIGUITY
                {
                    EnableCpuMemArena = true,
                    EnableMemoryPattern = true,
                    GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_EXTENDED
                };

                _mlpSession = new InferenceSession(modelPath, sessionOptions);
                _logger.LogInformation("✅ MLP model loaded successfully!");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to load MLP model");
                _mlpSession?.Dispose();
                _mlpSession = null;
            }
        }

        private async Task<DenseTensor<float>> CreateSymptomFeatureVectorAsync(List<int> symptomIds)
        {
            await Task.Delay(10); // Simulate DB lookup

            var tensor = new DenseTensor<float>(new[] { 1, FeatureSize });

            // Simple feature encoding - set 1.0 for present symptoms
            foreach (var symptomId in symptomIds.Take(FeatureSize))
            {
                if (symptomId > 0 && symptomId <= FeatureSize)
                {
                    tensor[0, symptomId - 1] = 1.0f;
                }
            }

            return tensor;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _mlpSession?.Dispose();
                _disposed = true;
                _logger.LogInformation("🔄 MLP Service disposed");
            }
        }
    }
}