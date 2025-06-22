// ===================================================================
// 1. Install only these packages (minimal dependencies)
// ===================================================================
/*
dotnet add package Microsoft.ML.OnnxRuntime --version 1.16.3
dotnet add package SixLabors.ImageSharp --version 3.0.2
*/

// ===================================================================
// 2. Simplified RealPredictionService.cs - No Tensor conflicts
// ===================================================================
using CoffeeDiseaseAnalysis.Models.DTOs;
using CoffeeDiseaseAnalysis.Services.Interfaces;
using Microsoft.ML.OnnxRuntime;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace CoffeeDiseaseAnalysis.Services
{
    public class RealPredictionService : IPredictionService, IDisposable
    {
        private readonly ILogger<RealPredictionService> _logger;
        private readonly string _modelPath;
        private InferenceSession? _onnxSession;
        private readonly bool _isModelAvailable;

        // Coffee disease class labels (update based on your model)
        private readonly Dictionary<int, string> _classLabels = new()
        {
            { 0, "Healthy" },
            { 1, "Rust" },       // Rỉ sắt
            { 2, "Cercospora" }, // Đốm lá Cercospora
            { 3, "Miner" },      // Sâu khoang lá
            { 4, "Phoma" }       // Bệnh Phoma
        };

        // Treatment suggestions in Vietnamese
        private readonly Dictionary<string, string> _treatmentSuggestions = new()
        {
            { "Healthy", "Lá cây khỏe mạnh. Tiếp tục chăm sóc theo quy trình hiện tại." },
            { "Rust", "Sử dụng thuốc fungicide đồng. Cải thiện thông gió và giảm độ ẩm." },
            { "Cercospora", "Phun thuốc chống nấm Tebuconazole. Loại bỏ lá bệnh và cải thiện thoát nước." },
            { "Miner", "Sử dụng thuốc trừ sâu sinh học hoặc Abamectin. Loại bỏ lá bị tổn hại." },
            { "Phoma", "Áp dụng fungicide Azoxystrobin. Tăng cường dinh dưỡng cho cây." }
        };

        public RealPredictionService(ILogger<RealPredictionService> logger, IWebHostEnvironment env)
        {
            _logger = logger;
            _modelPath = Path.Combine(env.WebRootPath, "models", "coffee_resnet50_model_final.onnx");

            try
            {
                if (File.Exists(_modelPath))
                {
                    _logger.LogInformation("🤖 Loading ONNX model from: {ModelPath}", _modelPath);

                    var sessionOptions = new Microsoft.ML.OnnxRuntime.SessionOptions();
                    sessionOptions.LogSeverityLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_WARNING;

                    _onnxSession = new InferenceSession(_modelPath, sessionOptions);
                    _isModelAvailable = true;

                    _logger.LogInformation("✅ ONNX model loaded successfully");
                    LogModelInfo();
                }
                else
                {
                    _logger.LogWarning("⚠️ ONNX model file not found at: {ModelPath}", _modelPath);
                    _isModelAvailable = false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to load ONNX model");
                _isModelAvailable = false;
            }
        }

        public async Task<PredictionResult> PredictDiseaseAsync(byte[] imageBytes, string imagePath, List<int>? symptomIds = null)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                _logger.LogInformation("🔄 Starting REAL AI prediction for image: {ImagePath}", imagePath);

                if (!_isModelAvailable || _onnxSession == null)
                {
                    _logger.LogError("❌ ONNX model not available");
                    throw new InvalidOperationException("AI Model không khả dụng. Vui lòng kiểm tra file coffee_resnet50_model_final.onnx");
                }

                // ✅ USE REAL ONNX MODEL
                var prediction = await PredictWithONNXModel(imageBytes, imagePath);
                prediction.ProcessingTimeMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;

                _logger.LogInformation("✅ REAL AI prediction completed: {Disease} ({Confidence:P})",
                    prediction.DiseaseName, prediction.Confidence);

                return prediction;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error during REAL AI prediction");
                throw new InvalidOperationException($"Lỗi khi phân tích ảnh bằng AI: {ex.Message}", ex);
            }
        }

        private async Task<PredictionResult> PredictWithONNXModel(byte[] imageBytes, string imagePath)
        {
            try
            {
                // 1. Preprocess image to float array
                var inputData = await PreprocessImageToArrayAsync(imageBytes);

                // 2. Get input metadata
                var inputMeta = _onnxSession!.InputMetadata.First();
                var inputName = inputMeta.Key;
                var inputShape = inputMeta.Value.Dimensions.ToArray();

                _logger.LogInformation("📋 Model expects input '{Name}' with shape [{Shape}]",
                    inputName, string.Join("x", inputShape));

                // 3. Create tensor from array
                var inputTensor = new Microsoft.ML.OnnxRuntime.Tensors.DenseTensor<float>(inputData, inputShape);

                // 4. Create inputs
                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor(inputName, inputTensor)
                };

                // 5. Run inference
                _logger.LogInformation("🤖 Running ONNX inference...");
                using var outputs = _onnxSession.Run(inputs);

                // 6. Process outputs
                var prediction = ProcessModelOutput(outputs, imagePath);

                _logger.LogInformation("✅ ONNX inference completed successfully");
                return prediction;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ ONNX inference failed: {Error}", ex.Message);
                throw;
            }
        }

        private async Task<float[]> PreprocessImageToArrayAsync(byte[] imageBytes)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var image = Image.Load<Rgb24>(imageBytes);

                    // Resize to model input size (typically 224x224 for ResNet)
                    const int targetSize = 224;
                    image.Mutate(x => x.Resize(targetSize, targetSize));

                    // ImageNet normalization values
                    var mean = new[] { 0.485f, 0.456f, 0.406f };
                    var std = new[] { 0.229f, 0.224f, 0.225f };

                    // Create flat array [1, 3, 224, 224] = 150,528 elements
                    var totalElements = 1 * 3 * targetSize * targetSize;
                    var inputArray = new float[totalElements];

                    int index = 0;

                    // Fill array in CHW format (Channel, Height, Width)
                    // Red channel
                    for (int y = 0; y < targetSize; y++)
                    {
                        for (int x = 0; x < targetSize; x++)
                        {
                            var pixel = image[x, y];
                            inputArray[index++] = (pixel.R / 255.0f - mean[0]) / std[0];
                        }
                    }

                    // Green channel
                    for (int y = 0; y < targetSize; y++)
                    {
                        for (int x = 0; x < targetSize; x++)
                        {
                            var pixel = image[x, y];
                            inputArray[index++] = (pixel.G / 255.0f - mean[1]) / std[1];
                        }
                    }

                    // Blue channel
                    for (int y = 0; y < targetSize; y++)
                    {
                        for (int x = 0; x < targetSize; x++)
                        {
                            var pixel = image[x, y];
                            inputArray[index++] = (pixel.B / 255.0f - mean[2]) / std[2];
                        }
                    }

                    _logger.LogInformation("✅ Image preprocessed to array: {Length} elements [1x3x{Size}x{Size}]",
                        inputArray.Length, targetSize, targetSize);

                    return inputArray;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Image preprocessing failed");
                    throw;
                }
            });
        }

        private PredictionResult ProcessModelOutput(IDisposableReadOnlyCollection<DisposableNamedOnnxValue> outputs, string imagePath)
        {
            try
            {
                // Get the first output
                var output = outputs.First();
                var outputTensor = output.AsTensor<float>();
                var probabilities = outputTensor.ToArray();

                _logger.LogInformation("📊 Model output: {Count} values", probabilities.Length);
                _logger.LogInformation("📊 Raw probabilities: [{Probs}]",
                    string.Join(", ", probabilities.Take(Math.Min(5, probabilities.Length)).Select(p => p.ToString("F4"))));

                // Apply softmax to get probabilities
                var softmaxProbs = Softmax(probabilities);

                // Find the class with highest probability
                var maxIndex = 0;
                var maxProb = softmaxProbs[0];
                for (int i = 1; i < softmaxProbs.Length; i++)
                {
                    if (softmaxProbs[i] > maxProb)
                    {
                        maxProb = softmaxProbs[i];
                        maxIndex = i;
                    }
                }

                var confidence = maxProb;
                var diseaseName = _classLabels.ContainsKey(maxIndex) ? _classLabels[maxIndex] : "Unknown";

                // Convert to Vietnamese disease names
                var vietnameseName = ConvertToVietnamese(diseaseName);

                var result = new PredictionResult
                {
                    DiseaseName = vietnameseName,
                    Confidence = (decimal)confidence,
                    SeverityLevel = GetSeverityLevel(confidence, diseaseName),
                    TreatmentSuggestion = _treatmentSuggestions.GetValueOrDefault(diseaseName, "Cần tư vấn chuyên gia."),
                    Description = GetDiseaseDescription(diseaseName),
                    PredictionDate = DateTime.UtcNow,
                    ImagePath = imagePath,
                    IsRealAI = true,
                    ModelType = "ResNet50-ONNX-REAL",
                    ModelVersion = "coffee_resnet50_model_final"
                };

                _logger.LogInformation("🎯 Prediction: {Disease} (Confidence: {Confidence:P})", vietnameseName, confidence);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to process model output");
                throw;
            }
        }

        private static float[] Softmax(float[] values)
        {
            // Avoid overflow by subtracting max value
            var maxVal = values.Max();
            var exp = new float[values.Length];
            var sum = 0.0f;

            for (int i = 0; i < values.Length; i++)
            {
                exp[i] = (float)Math.Exp(values[i] - maxVal);
                sum += exp[i];
            }

            for (int i = 0; i < exp.Length; i++)
            {
                exp[i] /= sum;
            }

            return exp;
        }

        private static string ConvertToVietnamese(string englishName)
        {
            return englishName switch
            {
                "Healthy" => "Khỏe mạnh",
                "Rust" => "Rỉ sắt",
                "Cercospora" => "Đốm lá Cercospora",
                "Miner" => "Sâu khoang lá",
                "Phoma" => "Bệnh Phoma",
                _ => englishName
            };
        }

        private static string GetSeverityLevel(float confidence, string diseaseName)
        {
            if (diseaseName == "Healthy")
                return "None";

            return confidence switch
            {
                >= 0.8f => "High",
                >= 0.6f => "Medium",
                _ => "Low"
            };
        }

        private static string GetDiseaseDescription(string diseaseName)
        {
            return diseaseName switch
            {
                "Healthy" => "Lá cây có màu xanh tự nhiên, không có dấu hiệu bệnh tật.",
                "Rust" => "Bệnh rỉ sắt do nấm Hemileia vastatrix gây ra, xuất hiện đốm cam vàng ở mặt dưới lá.",
                "Cercospora" => "Bệnh đốm lá do nấm Cercospora coffeicola, tạo đốm nâu có viền vàng.",
                "Miner" => "Sâu khoang lá tạo đường hầm uốn khúc trong lá, làm lá héo và rụng.",
                "Phoma" => "Bệnh đốm lá do nấm Phoma, gây đốm nâu đen trên lá.",
                _ => "Không xác định được loại bệnh."
            };
        }

        public async Task<bool> IsModelAvailableAsync()
        {
            return await Task.FromResult(_isModelAvailable);
        }

        public async Task<ModelStatistics> GetModelStatsAsync()
        {
            return await Task.FromResult(new ModelStatistics
            {
                ModelType = "ResNet50-ONNX-REAL",
                Version = "coffee_resnet50_model_final",
                IsAvailable = _isModelAvailable,
                TotalPredictions = 0,
                AverageConfidence = 0.0,
                DiseaseDistribution = _classLabels.Values.ToDictionary(v => v, v => 0),
                AverageProcessingTime = 2000, // ONNX models are typically slower
                SuccessRate = _isModelAvailable ? 1.0 : 0.0,
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
                _logger.LogInformation("🔄 Starting REAL AI batch prediction for {Count} images", imageBytes.Count);

                for (int i = 0; i < imageBytes.Count; i++)
                {
                    try
                    {
                        var result = await PredictDiseaseAsync(imageBytes[i], imagePaths[i]);
                        response.Results.Add(result);
                        response.ProcessedImages++;

                        _logger.LogInformation("✅ Batch image {Index}/{Total} processed with REAL AI: {Disease}",
                            i + 1, imageBytes.Count, result.DiseaseName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "❌ Error processing batch image {Index} with REAL AI", i);
                        response.Errors.Add($"Image {i + 1}: {ex.Message}");
                    }
                }

                response.EndTime = DateTime.UtcNow;
                response.Status = response.Errors.Count == 0 ? "Completed" : "Partial";

                _logger.LogInformation("✅ REAL AI batch prediction completed: {Processed}/{Total} successful",
                    response.ProcessedImages, response.TotalImages);
            }
            catch (Exception ex)
            {
                response.Status = "Failed";
                response.Errors.Add($"REAL AI batch processing failed: {ex.Message}");
                _logger.LogError(ex, "❌ REAL AI batch prediction failed");
            }

            return response;
        }

        private void LogModelInfo()
        {
            if (_onnxSession == null) return;

            try
            {
                _logger.LogInformation("📋 ONNX Model Information:");
                _logger.LogInformation("  - Input Count: {Count}", _onnxSession.InputMetadata.Count);
                _logger.LogInformation("  - Output Count: {Count}", _onnxSession.OutputMetadata.Count);

                foreach (var input in _onnxSession.InputMetadata)
                {
                    var dims = input.Value.Dimensions?.ToArray() ?? new int[0];
                    _logger.LogInformation("  - Input '{Name}': {Type} [{Shape}]",
                        input.Key,
                        input.Value.ElementType,
                        string.Join("x", dims));
                }

                foreach (var output in _onnxSession.OutputMetadata)
                {
                    var dims = output.Value.Dimensions?.ToArray() ?? new int[0];
                    _logger.LogInformation("  - Output '{Name}': {Type} [{Shape}]",
                        output.Key,
                        output.Value.ElementType,
                        string.Join("x", dims));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("⚠️ Could not log model info: {Error}", ex.Message);
            }
        }

        public void Dispose()
        {
            try
            {
                _onnxSession?.Dispose();
                _logger.LogInformation("🔄 REAL AI Model service disposed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error disposing ONNX session");
            }
        }
    }
}

