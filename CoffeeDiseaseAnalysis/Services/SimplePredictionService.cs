using CoffeeDiseaseAnalysis.Models.DTOs;
using CoffeeDiseaseAnalysis.Services.Interfaces;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace CoffeeDiseaseAnalysis.Services
{
    public class SimplePredictionService : IPredictionService
    {
        private readonly ILogger<SimplePredictionService> _logger;
        private readonly IWebHostEnvironment _env;
        private InferenceSession? _session;
        private readonly string _modelPath;
        private readonly Dictionary<int, string> _classLabels;

        public SimplePredictionService(ILogger<SimplePredictionService> logger, IWebHostEnvironment env)
        {
            _logger = logger;
            _env = env;
            _modelPath = Path.Combine(_env.WebRootPath, "models", "coffee_resnet50_model.onnx");

            // Định nghĩa các lớp bệnh
            _classLabels = new Dictionary<int, string>
            {
                { 0, "Cercospora" },
                { 1, "Healthy" },
                { 2, "Miner" },
                { 3, "Phoma" },
                { 4, "Rust" }
            };

            InitializeModel();
        }

        private void InitializeModel()
        {
            try
            {
                if (File.Exists(_modelPath))
                {
                    _session = new InferenceSession(_modelPath);
                    _logger.LogInformation("✅ Model loaded successfully from: {ModelPath}", _modelPath);
                }
                else
                {
                    _logger.LogWarning("⚠️ Model file not found at: {ModelPath}", _modelPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to load model");
            }
        }

        public async Task<PredictionResult> PredictDiseaseAsync(byte[] imageBytes, string imagePath, List<int>? symptomIds = null)
        {
            try
            {
                _logger.LogInformation("🔄 Starting disease prediction for image: {ImagePath}", imagePath);

                // Kiểm tra nếu model không có sẵn, trả về mock result
                if (_session == null)
                {
                    _logger.LogWarning("⚠️ Model not available, returning mock prediction");
                    return CreateMockPrediction(imagePath);
                }

                // Tiền xử lý ảnh
                var processedImage = await PreprocessImageAsync(imageBytes);

                // Thực hiện dự đoán
                var results = await RunInferenceAsync(processedImage);

                // Xử lý kết quả
                var prediction = ProcessResults(results, imagePath);

                _logger.LogInformation("✅ Prediction completed: {Disease} ({Confidence:P})",
                    prediction.DiseaseName, prediction.Confidence);

                return prediction;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error during prediction");
                return CreateMockPrediction(imagePath);
            }
        }

        private async Task<float[,,,]> PreprocessImageAsync(byte[] imageBytes)
        {
            using var image = SixLabors.ImageSharp.Image.Load<Rgb24>(imageBytes);

            // Resize to 224x224 (ResNet50 input size)
            image.Mutate(x => x.Resize(224, 224));

            // Convert to tensor format [1, 3, 224, 224]
            var tensor = new float[1, 3, 224, 224];

            for (int y = 0; y < 224; y++)
            {
                for (int x = 0; x < 224; x++)
                {
                    var pixel = image[x, y];

                    // Normalize to [0, 1] and apply ImageNet normalization
                    tensor[0, 0, y, x] = (pixel.R / 255.0f - 0.485f) / 0.229f; // Red
                    tensor[0, 1, y, x] = (pixel.G / 255.0f - 0.456f) / 0.224f; // Green  
                    tensor[0, 2, y, x] = (pixel.B / 255.0f - 0.406f) / 0.225f; // Blue
                }
            }

            return tensor;
        }

        private async Task<float[]> RunInferenceAsync(float[,,,] inputTensor)
        {
            return await Task.Run(() =>
            {
                var inputMeta = _session!.InputMetadata;
                var inputName = inputMeta.Keys.First();

                // Create ONNX tensor
                var dimensions = new int[] { 1, 3, 224, 224 };
                var tensor = new DenseTensor<float>(inputTensor.Cast<float>().ToArray(), dimensions);

                // Run inference
                var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(inputName, tensor) };
                var results = _session.Run(inputs);

                // Get output
                var output = results.First().AsEnumerable<float>().ToArray();
                return output;
            });
        }

        private PredictionResult ProcessResults(float[] rawResults, string imagePath)
        {
            // Apply softmax để chuyển logits thành probabilities
            var probabilities = Softmax(rawResults);

            // Tìm class có xác suất cao nhất
            var maxIndex = Array.IndexOf(probabilities, probabilities.Max());
            var maxConfidence = probabilities[maxIndex];

            var diseaseName = _classLabels[maxIndex];
            var severityLevel = GetSeverityLevel(maxConfidence);
            var treatmentSuggestion = GetTreatmentSuggestion(diseaseName);

            return new PredictionResult
            {
                DiseaseName = diseaseName,
                Confidence = (decimal)maxConfidence,
                SeverityLevel = severityLevel,
                TreatmentSuggestion = treatmentSuggestion,
                PredictionDate = DateTime.UtcNow,
                ImagePath = imagePath
            };
        }

        private float[] Softmax(float[] logits)
        {
            var max = logits.Max();
            var exp = logits.Select(x => Math.Exp(x - max)).ToArray();
            var sum = exp.Sum();
            return exp.Select(x => (float)(x / sum)).ToArray();
        }

        private string GetSeverityLevel(float confidence)
        {
            return confidence switch
            {
                >= 0.8f => "Mild",
                >= 0.6f => "Moderate",
                _ => "Severe"
            };
        }

        private string GetTreatmentSuggestion(string diseaseName)
        {
            return diseaseName switch
            {
                "Cercospora" => "Sử dụng thuốc trừ nấm chứa copper oxychloride. Cải thiện thông gió và tránh tưới nước lên lá.",
                "Rust" => "Áp dụng thuốc trừ nấm systemic. Loại bỏ lá bị nhiễm và cải thiện circulation không khí.",
                "Phoma" => "Sử dụng fungicide chứa mancozeb. Tránh độ ẩm cao và đảm bảo drainage tốt.",
                "Miner" => "Sử dụng insecticide chứa imidacloprid. Loại bỏ lá bị nhiễm và kiểm soát côn trùng.",
                "Healthy" => "Cây khỏe mạnh. Tiếp tục duy trì chế độ chăm sóc hiện tại.",
                _ => "Tham khảo ý kiến chuyên gia để có phương án điều trị phù hợp."
            };
        }

        private PredictionResult CreateMockPrediction(string imagePath)
        {
            var random = new Random();
            var diseases = new[] { "Cercospora", "Healthy", "Miner", "Phoma", "Rust" };
            var selectedDisease = diseases[random.Next(diseases.Length)];
            var confidence = 0.75m + (decimal)(random.NextDouble() * 0.2); // 0.75-0.95

            return new PredictionResult
            {
                DiseaseName = selectedDisease,
                Confidence = confidence,
                SeverityLevel = GetSeverityLevel((float)confidence),
                TreatmentSuggestion = GetTreatmentSuggestion(selectedDisease),
                PredictionDate = DateTime.UtcNow,
                ImagePath = imagePath
            };
        }

        public async Task<bool> IsModelAvailableAsync()
        {
            return await Task.FromResult(_session != null);
        }

        public async Task<Dictionary<string, object>> GetModelStatsAsync()
        {
            return await Task.FromResult(new Dictionary<string, object>
            {
                ["ModelPath"] = _modelPath,
                ["ModelLoaded"] = _session != null,
                ["SupportedClasses"] = _classLabels.Values.ToList(),
                ["InputSize"] = "224x224x3",
                ["ModelType"] = "ResNet50-ONNX"
            });
        }

        public void Dispose()
        {
            _session?.Dispose();
        }
    }
}