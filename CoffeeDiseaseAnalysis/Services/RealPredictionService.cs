// ===================================================================
// Enhanced RealPredictionService.cs với Advanced Image Preprocessing
// Áp dụng các kỹ thuật từ Python app.py
// ===================================================================
using CoffeeDiseaseAnalysis.Models.DTOs;
using CoffeeDiseaseAnalysis.Services.Interfaces;
using Microsoft.ML.OnnxRuntime;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Filters;

namespace CoffeeDiseaseAnalysis.Services
{
    public class RealPredictionService : IPredictionService, IDisposable
    {
        private readonly ILogger<RealPredictionService> _logger;
        private readonly string _modelPath;
        private InferenceSession? _onnxSession;
        private readonly bool _isModelAvailable;

        // Coffee disease class labels - Updated to match Python model
        private readonly Dictionary<int, string> _classLabels = new()
        {
            { 0, "Bệnh cercospora" },
            { 1, "Cây khoẻ (không bệnh)" },
            { 2, "Bệnh miner" },
            { 3, "Bệnh phoma" },
            { 4, "Bệnh gỉ sắt" }
        };

        // Treatment suggestions in Vietnamese
        private readonly Dictionary<string, string> _treatmentSuggestions = new()
        {
            { "Cây khoẻ (không bệnh)", "Lá cây khỏe mạnh. Tiếp tục chăm sóc theo quy trình hiện tại." },
            { "Bệnh gỉ sắt", "Sử dụng thuốc fungicide đồng. Cải thiện thông gió và giảm độ ẩm." },
            { "Bệnh cercospora", "Phun thuốc chống nấm Tebuconazole. Loại bỏ lá bệnh và cải thiện thoát nước." },
            { "Bệnh miner", "Sử dụng thuốc trừ sâu sinh học hoặc Abamectin. Loại bỏ lá bị tổn hại." },
            { "Bệnh phoma", "Áp dụng fungicide Azoxystrobin. Tăng cường dinh dưỡng cho cây." }
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
                _logger.LogInformation("🔄 Starting ENHANCED AI prediction for image: {ImagePath}", imagePath);

                if (!_isModelAvailable || _onnxSession == null)
                {
                    _logger.LogError("❌ ONNX model not available");
                    throw new InvalidOperationException("AI Model không khả dụng. Vui lòng kiểm tra file coffee_resnet50_model_final.onnx");
                }

                // ✅ USE ENHANCED PREPROCESSING
                var prediction = await PredictWithEnhancedONNXModel(imageBytes, imagePath);
                prediction.ProcessingTimeMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;

                _logger.LogInformation("✅ ENHANCED AI prediction completed: {Disease} ({Confidence:P})",
                    prediction.DiseaseName, prediction.Confidence);

                return prediction;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error during ENHANCED AI prediction");
                throw new InvalidOperationException($"Lỗi khi phân tích ảnh bằng AI: {ex.Message}", ex);
            }
        }

        private async Task<PredictionResult> PredictWithEnhancedONNXModel(byte[] imageBytes, string imagePath)
        {
            try
            {
                // 1. ENHANCED PREPROCESSING with quality detection
                var (inputData, qualityScore, imageInfo) = await PreprocessImageAdvancedAsync(imageBytes);

                _logger.LogInformation("📊 Image quality score: {Quality:F2}, Size: {Width}x{Height}",
                    qualityScore, imageInfo.Width, imageInfo.Height);

                // 2. Get input metadata
                var inputMeta = _onnxSession!.InputMetadata.First();
                var inputName = inputMeta.Key;
                var rawShape = inputMeta.Value.Dimensions.ToArray();

                _logger.LogInformation("📋 Model raw input shape: [{RawShape}]",
                    string.Join("x", rawShape));

                // 3. FIX: Handle dynamic dimensions
                var inputShape = FixInputShape(rawShape, inputData.Length);

                _logger.LogInformation("📋 Fixed input shape: [{Shape}]",
                    string.Join("x", inputShape));

                // 4. Validate input data matches expected shape
                var expectedSize = inputShape.Aggregate(1, (a, b) => a * b);
                if (inputData.Length != expectedSize)
                {
                    throw new InvalidOperationException(
                        $"Input data size mismatch. Expected: {expectedSize}, Got: {inputData.Length}");
                }

                // 5. Create tensor from array with fixed shape
                var inputTensor = new Microsoft.ML.OnnxRuntime.Tensors.DenseTensor<float>(inputData, inputShape);

                // 6. Create inputs
                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor(inputName, inputTensor)
                };

                // 7. Run inference
                _logger.LogInformation("🤖 Running ENHANCED ONNX inference...");
                using var outputs = _onnxSession.Run(inputs);

                // 8. Process outputs with quality consideration
                var prediction = ProcessModelOutputAdvanced(outputs, imagePath, qualityScore);

                _logger.LogInformation("✅ ENHANCED ONNX inference completed successfully");
                return prediction;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ ENHANCED ONNX inference failed: {Error}", ex.Message);
                throw;
            }
        }

        // ===============================================
        // ENHANCED IMAGE PREPROCESSING (From Python app.py)
        // ===============================================

        private async Task<(float[] data, float qualityScore, ImageInfo info)> PreprocessImageAdvancedAsync(byte[] imageBytes)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var image = Image.Load<Rgb24>(imageBytes);
                    var originalWidth = image.Width;
                    var originalHeight = image.Height;

                    _logger.LogInformation("📷 Original image size: {Width}x{Height}", originalWidth, originalHeight);

                    // 1. DETECT IMAGE QUALITY (from Python)
                    var qualityScore = DetectImageQuality(image);

                    // 2. ENHANCE IMAGE QUALITY if needed (from Python enhance_image_quality)
                    if (qualityScore < 0.7f)
                    {
                        _logger.LogInformation("⚡ Enhancing image quality (score: {Score:F2})", qualityScore);
                        var enhancedImage = EnhanceImageQuality(image);
                        image.Dispose(); // Dispose original
                        image = enhancedImage; // Assign new enhanced image
                    }

                    // 3. RESIZE with LANCZOS (from Python)
                    const int targetSize = 224;
                    image.Mutate(x => x.Resize(targetSize, targetSize, KnownResamplers.Lanczos3));

                    // 4. RESNET50 PREPROCESSING (exactly like Python tf.keras.applications.resnet50.preprocess_input)
                    var inputArray = ApplyResNet50Preprocessing(image, targetSize);

                    var imageInfo = new ImageInfo
                    {
                        Width = originalWidth,
                        Height = originalHeight,
                        QualityScore = qualityScore
                    };

                    _logger.LogInformation("✅ Enhanced image preprocessed: {Length} elements, quality: {Quality:F2}",
                        inputArray.Length, qualityScore);

                    // Dispose image after processing
                    image.Dispose();

                    return (inputArray, qualityScore, imageInfo);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Enhanced image preprocessing failed");
                    throw;
                }
            });
        }

        private float DetectImageQuality(Image<Rgb24> image)
        {
            try
            {
                // Convert to grayscale for analysis
                using var grayImage = image.Clone();
                grayImage.Mutate(x => x.Grayscale());

                var pixels = new byte[grayImage.Width * grayImage.Height];
                grayImage.CopyPixelDataTo(pixels);

                // 1. Brightness analysis (from Python)
                var brightness = pixels.Average(p => (float)p);
                var brightnessIssue = brightness < 50 || brightness > 200;

                // 2. Contrast analysis (from Python)
                var mean = brightness;
                var variance = pixels.Sum(p => Math.Pow(p - mean, 2)) / pixels.Length;
                var contrast = Math.Sqrt(variance);
                var lowContrast = contrast < 20;

                // 3. Blur detection (simplified Laplacian variance)
                var blurScore = CalculateBlurScore(pixels, grayImage.Width, grayImage.Height);
                var isBlurry = blurScore < 100;

                // Calculate quality score (from Python logic)
                var qualityScore = 1.0f;
                if (isBlurry) qualityScore *= 0.5f;
                if (brightnessIssue) qualityScore *= 0.7f;
                if (lowContrast) qualityScore *= 0.8f;

                _logger.LogInformation("📊 Quality analysis - Brightness: {Brightness:F1}, Contrast: {Contrast:F1}, Blur: {Blur:F1}, Score: {Score:F2}",
                    brightness, contrast, blurScore, qualityScore);

                return qualityScore;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("⚠️ Quality detection failed: {Error}", ex.Message);
                return 0.8f; // Default decent quality
            }
        }

        private float CalculateBlurScore(byte[] pixels, int width, int height)
        {
            // Simplified Laplacian variance for blur detection
            var laplacianSum = 0.0;
            var count = 0;

            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    var center = pixels[y * width + x];
                    var laplacian = Math.Abs(8 * center
                        - pixels[(y - 1) * width + x - 1]
                        - pixels[(y - 1) * width + x]
                        - pixels[(y - 1) * width + x + 1]
                        - pixels[y * width + x - 1]
                        - pixels[y * width + x + 1]
                        - pixels[(y + 1) * width + x - 1]
                        - pixels[(y + 1) * width + x]
                        - pixels[(y + 1) * width + x + 1]);

                    laplacianSum += laplacian * laplacian;
                    count++;
                }
            }

            return count > 0 ? (float)(laplacianSum / count) : 0;
        }

        private Image<Rgb24> EnhanceImageQuality(Image<Rgb24> originalImage)
        {
            try
            {
                // Create a copy to avoid modifying the original
                var enhancedImage = originalImage.Clone();

                // Apply image enhancements (inspired by Python CLAHE and bilateral filter)
                enhancedImage.Mutate(x => x
                    .GaussianSharpen(1.0f) // Sharpen
                    .Contrast(1.2f)        // Increase contrast
                    .Brightness(1.1f)      // Slight brightness boost
                );

                _logger.LogInformation("⚡ Applied image quality enhancements");
                return enhancedImage;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("⚠️ Image enhancement failed: {Error}", ex.Message);
                return originalImage.Clone(); // Return copy of original if enhancement fails
            }
        }

        private float[] ApplyResNet50Preprocessing(Image<Rgb24> image, int targetSize)
        {
            // EXACT ResNet50 preprocessing like Python tf.keras.applications.resnet50.preprocess_input
            var totalElements = 1 * 3 * targetSize * targetSize;
            var inputArray = new float[totalElements];

            int index = 0;

            // ImageNet means for ResNet50 (from Keras source)
            var means = new[] { 103.939f, 116.779f, 123.68f }; // BGR order

            // NO normalization by std, just subtract mean (ResNet50 style)
            // Fill array in CHW format (Channel, Height, Width)

            // Blue channel (index 0 in ResNet50)
            for (int y = 0; y < targetSize; y++)
            {
                for (int x = 0; x < targetSize; x++)
                {
                    var pixel = image[x, y];
                    inputArray[index++] = pixel.B - means[0]; // Blue - mean
                }
            }

            // Green channel (index 1 in ResNet50)
            for (int y = 0; y < targetSize; y++)
            {
                for (int x = 0; x < targetSize; x++)
                {
                    var pixel = image[x, y];
                    inputArray[index++] = pixel.G - means[1]; // Green - mean
                }
            }

            // Red channel (index 2 in ResNet50)
            for (int y = 0; y < targetSize; y++)
            {
                for (int x = 0; x < targetSize; x++)
                {
                    var pixel = image[x, y];
                    inputArray[index++] = pixel.R - means[2]; // Red - mean
                }
            }

            _logger.LogInformation("✅ Applied ResNet50 preprocessing (subtract ImageNet means)");
            return inputArray;
        }

        // ===============================================
        // ENHANCED OUTPUT PROCESSING
        // ===============================================

        private PredictionResult ProcessModelOutputAdvanced(IDisposableReadOnlyCollection<DisposableNamedOnnxValue> outputs, string imagePath, float qualityScore)
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

                // QUALITY-ADJUSTED CONFIDENCE
                var adjustedConfidence = AdjustConfidenceByQuality(confidence, qualityScore);

                var result = new PredictionResult
                {
                    DiseaseName = diseaseName,
                    Confidence = (decimal)adjustedConfidence,
                    SeverityLevel = GetSeverityLevel(adjustedConfidence, diseaseName),
                    TreatmentSuggestion = _treatmentSuggestions.GetValueOrDefault(diseaseName, "Cần tư vấn chuyên gia."),
                    Description = GetDiseaseDescription(diseaseName),
                    PredictionDate = DateTime.UtcNow,
                    ImagePath = imagePath,
                    IsRealAI = true,
                    ModelType = "ResNet50-ONNX-ENHANCED",
                    ModelVersion = "coffee_resnet50_model_final_v2"
                };

                // Add quality warnings
                if (qualityScore < 0.7f)
                {
                    result.Description += $" (Chất lượng ảnh: {qualityScore:P0} - kết quả có thể không chính xác)";
                }

                _logger.LogInformation("🎯 Enhanced Prediction: {Disease} (Confidence: {Confidence:P}, Quality: {Quality:P})",
                    diseaseName, adjustedConfidence, qualityScore);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to process enhanced model output");
                throw;
            }
        }

        private float AdjustConfidenceByQuality(float originalConfidence, float qualityScore)
        {
            // Adjust confidence based on image quality (like Python logic)
            if (qualityScore < 0.5f)
            {
                return originalConfidence * 0.7f; // Reduce confidence for very poor quality
            }
            else if (qualityScore < 0.7f)
            {
                return originalConfidence * 0.85f; // Slight reduction for poor quality
            }

            return originalConfidence; // Keep original for good quality
        }

        // ===============================================
        // HELPER CLASSES AND EXISTING METHODS
        // ===============================================

        private class ImageInfo
        {
            public int Width { get; set; }
            public int Height { get; set; }
            public float QualityScore { get; set; }
        }

        private int[] FixInputShape(int[] rawShape, int dataLength)
        {
            var fixedShape = new int[rawShape.Length];

            for (int i = 0; i < rawShape.Length; i++)
            {
                if (rawShape[i] <= 0) // Handle -1 or other invalid dimensions
                {
                    switch (i)
                    {
                        case 0: fixedShape[i] = 1; break;     // Batch size
                        case 1: fixedShape[i] = 3; break;     // Channels (RGB)
                        case 2:
                        case 3: fixedShape[i] = 224; break;   // Height/Width
                        default: fixedShape[i] = 1; break;    // Default fallback
                    }

                    _logger.LogWarning("⚠️ Fixed dynamic dimension at index {Index}: {Old} -> {New}",
                        i, rawShape[i], fixedShape[i]);
                }
                else
                {
                    fixedShape[i] = rawShape[i];
                }
            }

            var calculatedSize = fixedShape.Aggregate(1, (a, b) => a * b);

            if (calculatedSize != dataLength)
            {
                _logger.LogWarning("⚠️ Shape mismatch after fix. Trying fallback shape [1,3,224,224]");

                if (dataLength == 1 * 3 * 224 * 224) // 150,528 elements
                {
                    return new int[] { 1, 3, 224, 224 };
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Cannot determine valid input shape. Data length: {dataLength}, " +
                        $"Calculated size: {calculatedSize}, Raw shape: [{string.Join("x", rawShape)}]");
                }
            }

            return fixedShape;
        }

        private static float[] Softmax(float[] values)
        {
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

        private static string GetSeverityLevel(float confidence, string diseaseName)
        {
            if (diseaseName == "Cây khoẻ (không bệnh)")
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
                "Cây khoẻ (không bệnh)" => "Lá cây có màu xanh tự nhiên, không có dấu hiệu bệnh tật.",
                "Bệnh gỉ sắt" => "Bệnh rỉ sắt do nấm Hemileia vastatrix gây ra, xuất hiện đốm cam vàng ở mặt dưới lá.",
                "Bệnh cercospora" => "Bệnh đốm lá do nấm Cercospora coffeicola, tạo đốm nâu có viền vàng.",
                "Bệnh miner" => "Sâu khoang lá tạo đường hầm uốn khúc trong lá, làm lá héo và rụng.",
                "Bệnh phoma" => "Bệnh đốm lá do nấm Phoma, gây đốm nâu đen trên lá.",
                _ => "Không xác định được loại bệnh."
            };
        }

        // ===============================================
        // EXISTING INTERFACE METHODS
        // ===============================================

        public async Task<bool> IsModelAvailableAsync()
        {
            return await Task.FromResult(_isModelAvailable);
        }

        public async Task<ModelStatistics> GetModelStatsAsync()
        {
            return await Task.FromResult(new ModelStatistics
            {
                ModelType = "ResNet50-ONNX-ENHANCED",
                Version = "coffee_resnet50_model_final_v2",
                IsAvailable = _isModelAvailable,
                TotalPredictions = 0,
                AverageConfidence = 0.0,
                DiseaseDistribution = _classLabels.Values.ToDictionary(v => v, v => 0),
                AverageProcessingTime = 2500, // Slightly higher due to enhanced processing
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
                _logger.LogInformation("🔄 Starting ENHANCED AI batch prediction for {Count} images", imageBytes.Count);

                for (int i = 0; i < imageBytes.Count; i++)
                {
                    try
                    {
                        var result = await PredictDiseaseAsync(imageBytes[i], imagePaths[i]);
                        response.Results.Add(result);
                        response.ProcessedImages++;

                        _logger.LogInformation("✅ Batch image {Index}/{Total} processed with ENHANCED AI: {Disease}",
                            i + 1, imageBytes.Count, result.DiseaseName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "❌ Error processing batch image {Index} with ENHANCED AI", i);
                        response.Errors.Add($"Image {i + 1}: {ex.Message}");
                    }
                }

                response.EndTime = DateTime.UtcNow;
                response.Status = response.Errors.Count == 0 ? "Completed" : "Partial";

                _logger.LogInformation("✅ ENHANCED AI batch prediction completed: {Processed}/{Total} successful",
                    response.ProcessedImages, response.TotalImages);
            }
            catch (Exception ex)
            {
                response.Status = "Failed";
                response.Errors.Add($"ENHANCED AI batch processing failed: {ex.Message}");
                _logger.LogError(ex, "❌ ENHANCED AI batch prediction failed");
            }

            return response;
        }

        private void LogModelInfo()
        {
            if (_onnxSession == null) return;

            try
            {
                _logger.LogInformation("📋 ENHANCED ONNX Model Information:");
                _logger.LogInformation("  - Input Count: {Count}", _onnxSession.InputMetadata.Count);
                _logger.LogInformation("  - Output Count: {Count}", _onnxSession.OutputMetadata.Count);

                foreach (var input in _onnxSession.InputMetadata)
                {
                    var dims = input.Value.Dimensions?.ToArray() ?? new int[0];
                    var dimsStr = string.Join("x", dims.Select(d => d <= 0 ? "?" : d.ToString()));

                    _logger.LogInformation("  - Input '{Name}': {Type} [{Shape}]",
                        input.Key,
                        input.Value.ElementType,
                        dimsStr);

                    if (dims.Any(d => d <= 0))
                    {
                        _logger.LogWarning("  ⚠️ Input '{Name}' has dynamic dimensions that will be fixed at runtime",
                            input.Key);
                    }
                }

                foreach (var output in _onnxSession.OutputMetadata)
                {
                    var dims = output.Value.Dimensions?.ToArray() ?? new int[0];
                    var dimsStr = string.Join("x", dims.Select(d => d <= 0 ? "?" : d.ToString()));

                    _logger.LogInformation("  - Output '{Name}': {Type} [{Shape}]",
                        output.Key,
                        output.Value.ElementType,
                        dimsStr);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("⚠️ Could not log enhanced model info: {Error}", ex.Message);
            }
        }

        public void Dispose()
        {
            try
            {
                _onnxSession?.Dispose();
                _logger.LogInformation("🔄 ENHANCED AI Model service disposed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error disposing enhanced ONNX session");
            }
        }
    }
}