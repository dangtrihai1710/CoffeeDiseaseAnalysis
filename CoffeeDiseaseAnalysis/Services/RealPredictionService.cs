// ===================================================================
// REAL COFFEE BACKEND - NO MOCK, REAL AI RESULTS ONLY
// ===================================================================

// File: CoffeeDiseaseAnalysis/Services/RealPredictionService.cs
using CoffeeDiseaseAnalysis.Models.DTOs;
using CoffeeDiseaseAnalysis.Services.Interfaces;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace CoffeeDiseaseAnalysis.Services
{
    public class RealPredictionService : IPredictionService, IDisposable
    {
        private readonly ILogger<RealPredictionService> _logger;
        private readonly IWebHostEnvironment _env;
        private InferenceSession? _session;
        private readonly string _modelPath;
        private readonly Dictionary<int, string> _classLabels;
        private readonly Dictionary<string, string> _diseaseDescriptions;
        private readonly Dictionary<string, string> _treatmentSuggestions;

        public RealPredictionService(ILogger<RealPredictionService> logger, IWebHostEnvironment env)
        {
            _logger = logger;
            _env = env;
            _modelPath = Path.Combine(_env.WebRootPath, "models", "coffee_resnet50_model_final.onnx");

            // Class labels khớp với model đã train
            _classLabels = new Dictionary<int, string>
            {
                { 0, "Cercospora" },
                { 1, "Healthy" },
                { 2, "Miner" },
                { 3, "Phoma" },
                { 4, "Rust" }
            };

            InitializeDiseaseData();
            InitializeModel();
        }

        private void InitializeDiseaseData()
        {
            _diseaseDescriptions = new Dictionary<string, string>
            {
                ["Cercospora"] = "Bệnh đốm nâu Cercospora là bệnh nấm phổ biến trên cây cà phê, gây ra các vết đốm tròn màu nâu có đường viền rõ ràng trên lá.",
                ["Healthy"] = "Lá cây cà phê khỏe mạnh, màu xanh tươi, không có dấu hiệu bệnh tật hay sâu hại.",
                ["Miner"] = "Sâu đục lá (Leaf Miner) tạo ra các đường hầm màu trắng uốn khúc bên trong tổ chức lá.",
                ["Phoma"] = "Bệnh đốm đen Phoma gây ra các vết đốm đen có viền vàng, thường xuất hiện ở mép lá.",
                ["Rust"] = "Bệnh rỉ sắt cà phê gây ra các đốm màu cam/vàng ở mặt dưới lá, là bệnh nguy hiểm nhất của cà phê."
            };

            _treatmentSuggestions = new Dictionary<string, string>
            {
                ["Cercospora"] = "Sử dụng thuốc trừ nấm chứa hoạt chất copper oxychloride hoặc mancozeb. Cải thiện thông gió vườn, tránh tưới nước lên lá. Loại bỏ lá bị bệnh và tiêu hủy.",
                ["Healthy"] = "Cây khỏe mạnh! Tiếp tục duy trì chế độ chăm sóc hiện tại: tưới nước đủ ẩm, bón phân cân đối, cắt tỉa thông thoáng.",
                ["Miner"] = "Sử dụng thuốc trừ sâu chứa hoạt chất imidacloprid hoặc thiamethoxam. Loại bỏ lá bị nhiễm, sử dụng bẫy dính màu vàng để bắt sâu trưởng thành.",
                ["Phoma"] = "Sử dụng thuốc trừ nấm chứa propiconazole hoặc azoxystrobin. Tránh độ ẩm cao, đảm bảo thoát nước tốt, cắt tỉa cành khô và lá bệnh.",
                ["Rust"] = "ẤP DỤNG NGAY: Sử dụng thuốc trừ nấm hệ thống chứa triazole. Loại bỏ tất cả lá bị nhiễm, cải thiện lưu thông không khí. Đây là bệnh rất nguy hiểm cần xử lý gấp!"
            };
        }

        private void InitializeModel()
        {
            try
            {
                if (!File.Exists(_modelPath))
                {
                    _logger.LogError("❌ Model file not found at: {ModelPath}", _modelPath);
                    _logger.LogError("📁 Please ensure coffee_resnet50_model_final.onnx is placed in wwwroot/models/");
                    return;
                }

                var sessionOptions = new SessionOptions();
                sessionOptions.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;

                _session = new InferenceSession(_modelPath, sessionOptions);

                _logger.LogInformation("✅ REAL AI Model loaded successfully from: {ModelPath}", _modelPath);
                _logger.LogInformation("📊 Input metadata: {InputMeta}", string.Join(", ", _session.InputMetadata.Keys));
                _logger.LogInformation("📊 Output metadata: {OutputMeta}", string.Join(", ", _session.OutputMetadata.Keys));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to load REAL AI model");
                _session = null;
            }
        }

        public async Task<PredictionResult> PredictDiseaseAsync(byte[] imageBytes, string imagePath, List<int>? symptomIds = null)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                _logger.LogInformation("🔄 Starting REAL AI disease prediction for image: {ImagePath}", imagePath);

                if (_session == null)
                {
                    throw new InvalidOperationException("AI Model không khả dụng. Vui lòng kiểm tra file model coffee_resnet50_model_final.onnx trong thư mục wwwroot/models/");
                }

                // 1. Tiền xử lý ảnh cho ResNet50
                var preprocessedTensor = await PreprocessImageAsync(imageBytes);
                _logger.LogInformation("✅ Image preprocessing completed");

                // 2. Thực hiện inference với ONNX model
                var rawPredictions = await RunInferenceAsync(preprocessedTensor);
                _logger.LogInformation("✅ AI inference completed");

                // 3. Xử lý kết quả và áp dụng softmax
                var probabilities = ApplySoftmax(rawPredictions);

                // 4. Tìm class có xác suất cao nhất
                var maxIndex = Array.IndexOf(probabilities, probabilities.Max());
                var maxConfidence = probabilities[maxIndex];

                if (!_classLabels.TryGetValue(maxIndex, out var diseaseName))
                {
                    throw new InvalidOperationException($"Unknown class index: {maxIndex}");
                }

                // 5. Đánh giá độ tin cậy
                if (maxConfidence < 0.3m)
                {
                    _logger.LogWarning("⚠️ Low confidence prediction: {Confidence:P} for {Disease}", maxConfidence, diseaseName);
                }

                // 6. Tạo kết quả
                var result = new PredictionResult
                {
                    DiseaseName = diseaseName,
                    Confidence = (decimal)maxConfidence,
                    SeverityLevel = GetSeverityLevel((float)maxConfidence, diseaseName),
                    TreatmentSuggestion = _treatmentSuggestions[diseaseName],
                    Description = _diseaseDescriptions[diseaseName],
                    PredictionDate = DateTime.UtcNow,
                    ImagePath = imagePath,
                    ProcessingTimeMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds
                };

                _logger.LogInformation("✅ REAL AI Prediction completed: {Disease} ({Confidence:P2}) in {Time}ms",
                    result.DiseaseName, result.Confidence, result.ProcessingTimeMs);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error during REAL AI prediction");
                throw new InvalidOperationException($"Lỗi khi phân tích ảnh bằng AI: {ex.Message}", ex);
            }
        }

        private async Task<Tensor<float>> PreprocessImageAsync(byte[] imageBytes)
        {
            return await Task.Run(() =>
            {
                using var image = Image.Load<Rgb24>(imageBytes);

                // Resize to 224x224 (ResNet50 standard input)
                image.Mutate(x => x.Resize(224, 224));

                // Convert to tensor [1, 3, 224, 224] with ImageNet normalization
                var tensor = new DenseTensor<float>(new[] { 1, 3, 224, 224 });

                // ImageNet mean and std
                var mean = new[] { 0.485f, 0.456f, 0.406f };
                var std = new[] { 0.229f, 0.224f, 0.225f };

                for (int y = 0; y < 224; y++)
                {
                    for (int x = 0; x < 224; x++)
                    {
                        var pixel = image[x, y];

                        // Normalize and apply ImageNet preprocessing
                        tensor[0, 0, y, x] = ((pixel.R / 255.0f) - mean[0]) / std[0]; // Red
                        tensor[0, 1, y, x] = ((pixel.G / 255.0f) - mean[1]) / std[1]; // Green
                        tensor[0, 2, y, x] = ((pixel.B / 255.0f) - mean[2]) / std[2]; // Blue
                    }
                }

                return tensor;
            });
        }

        private async Task<float[]> RunInferenceAsync(Tensor<float> inputTensor)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Get input name từ model metadata
                    var inputName = _session!.InputMetadata.Keys.First();

                    // Tạo input cho ONNX session
                    var inputs = new List<NamedOnnxValue>
                    {
                        NamedOnnxValue.CreateFromTensor(inputName, inputTensor)
                    };

                    // Chạy inference
                    using var results = _session.Run(inputs);

                    // Lấy output (logits)
                    var output = results.First().AsEnumerable<float>().ToArray();

                    _logger.LogInformation("🔍 Raw model output: [{Output}]", string.Join(", ", output.Select(x => x.ToString("F4"))));

                    return output;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ ONNX Runtime error during inference");
                    throw;
                }
            });
        }

        private float[] ApplySoftmax(float[] logits)
        {
            // Apply softmax để convert logits thành probabilities
            var max = logits.Max();
            var exp = logits.Select(x => Math.Exp(x - max)).ToArray();
            var sum = exp.Sum();
            var probabilities = exp.Select(x => (float)(x / sum)).ToArray();

            _logger.LogInformation("🔢 Softmax probabilities: [{Probs}]",
                string.Join(", ", probabilities.Select((p, i) => $"{_classLabels[i]}: {p:P2}")));

            return probabilities;
        }

        private string GetSeverityLevel(float confidence, string diseaseName)
        {
            // Healthy không có severity
            if (diseaseName == "Healthy")
                return "None";

            // Rust là bệnh nguy hiểm nhất
            if (diseaseName == "Rust")
            {
                return confidence switch
                {
                    >= 0.7f => "Severe", // Rust với confidence cao = nguy hiểm
                    >= 0.5f => "Moderate",
                    _ => "Mild"
                };
            }

            // Các bệnh khác
            return confidence switch
            {
                >= 0.8f => "Mild",
                >= 0.6f => "Moderate",
                _ => "Severe" // Confidence thấp = khó chẩn đoán = có thể nghiêm trọng
            };
        }

        public async Task<bool> IsModelAvailableAsync()
        {
            return await Task.FromResult(_session != null);
        }

        public async Task<ModelStatistics> GetModelStatsAsync()
        {
            var isAvailable = _session != null;

            return await Task.FromResult(new ModelStatistics
            {
                ModelType = "ResNet50-ONNX",
                Version = "coffee_resnet50_model_final",
                IsAvailable = isAvailable,
                TotalPredictions = 0, // Sẽ được cập nhật từ database
                AverageConfidence = 0.0,
                DiseaseDistribution = _classLabels.Values.ToDictionary(v => v, v => 0),
                AverageProcessingTime = 0.0,
                SuccessRate = isAvailable ? 1.0 : 0.0,
                LastUsed = DateTime.UtcNow
            });
        }

        public async Task<BatchPredictionResponse> PredictBatchAsync(List<byte[]> imageBytes, List<string> imagePaths)
        {
            var response = new BatchPredictionResponse
            {
                BatchId = Guid.NewGuid().ToString(),
                TotalImages = imageBytes.Count,
                StartTime = DateTime.UtcNow,
                Status = "Processing"
            };

            try
            {
                _logger.LogInformation("🔄 Starting batch prediction for {Count} images", imageBytes.Count);

                for (int i = 0; i < imageBytes.Count; i++)
                {
                    try
                    {
                        var result = await PredictDiseaseAsync(imageBytes[i], imagePaths[i]);
                        response.Results.Add(result);
                        response.ProcessedImages++;

                        _logger.LogInformation("✅ Batch image {Index}/{Total} processed: {Disease}",
                            i + 1, imageBytes.Count, result.DiseaseName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "❌ Error processing batch image {Index}", i);
                        response.Errors.Add($"Image {i + 1}: {ex.Message}");
                    }
                }

                response.EndTime = DateTime.UtcNow;
                response.Status = response.Errors.Count == 0 ? "Completed" : "Partial";

                _logger.LogInformation("✅ Batch prediction completed: {Processed}/{Total} successful",
                    response.ProcessedImages, response.TotalImages);
            }
            catch (Exception ex)
            {
                response.Status = "Failed";
                response.Errors.Add($"Batch processing failed: {ex.Message}");
                _logger.LogError(ex, "❌ Batch prediction failed");
            }

            return response;
        }

        public void Dispose()
        {
            _session?.Dispose();
            _logger.LogInformation("🔄 REAL AI Model session disposed");
        }
    }
}