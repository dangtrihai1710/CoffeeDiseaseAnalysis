// File: CoffeeDiseaseAnalysis/Services/MessageQueueService.cs - FIXED
using CoffeeDiseaseAnalysis.Data;
using CoffeeDiseaseAnalysis.Data.Entities;
using CoffeeDiseaseAnalysis.Models.DTOs;
using CoffeeDiseaseAnalysis.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace CoffeeDiseaseAnalysis.Services
{
    public class MessageQueueService : IMessageQueueService, IDisposable
    {
        private readonly IConfiguration _configuration;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<MessageQueueService> _logger;

        private IConnection? _connection;
        private IModel? _channel;
        private EventingBasicConsumer? _consumer;
        private bool _disposed = false;

        private const string IMAGE_PROCESSING_QUEUE = "image-processing-queue";
        private const string PREDICTION_RESULT_QUEUE = "prediction-result-queue";
        private const string EXCHANGE_NAME = "coffee-disease-exchange";

        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public MessageQueueService(
            IConfiguration configuration,
            IServiceProvider serviceProvider,
            ILogger<MessageQueueService> logger)
        {
            _configuration = configuration;
            _serviceProvider = serviceProvider;
            _logger = logger;

            InitializeRabbitMQ();
        }

        private void InitializeRabbitMQ()
        {
            try
            {
                var factory = new ConnectionFactory()
                {
                    HostName = _configuration["RabbitMQ:HostName"] ?? "localhost",
                    Port = int.Parse(_configuration["RabbitMQ:Port"] ?? "5672"),
                    UserName = _configuration["RabbitMQ:UserName"] ?? "guest",
                    Password = _configuration["RabbitMQ:Password"] ?? "guest",
                    AutomaticRecoveryEnabled = true,
                    NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
                };

                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();

                // Declare exchange
                _channel.ExchangeDeclare(EXCHANGE_NAME, ExchangeType.Direct, durable: true);

                // Declare queues
                _channel.QueueDeclare(IMAGE_PROCESSING_QUEUE, durable: true, exclusive: false, autoDelete: false);
                _channel.QueueDeclare(PREDICTION_RESULT_QUEUE, durable: true, exclusive: false, autoDelete: false);

                // Bind queues
                _channel.QueueBind(IMAGE_PROCESSING_QUEUE, EXCHANGE_NAME, "image.processing");
                _channel.QueueBind(PREDICTION_RESULT_QUEUE, EXCHANGE_NAME, "prediction.result");

                // Set QoS
                _channel.BasicQos(0, 1, false);

                _logger.LogInformation("RabbitMQ initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize RabbitMQ");
                // Don't throw - allow service to work without MQ
            }
        }

        public async Task PublishImageProcessingRequestAsync(ImageProcessingRequest request)
        {
            try
            {
                if (_channel == null || !_channel.IsOpen)
                {
                    _logger.LogWarning("RabbitMQ channel not available, skipping publish");
                    return;
                }

                var json = JsonSerializer.Serialize(request, _jsonOptions);
                var body = Encoding.UTF8.GetBytes(json);

                var properties = _channel.CreateBasicProperties();
                properties.Persistent = true;
                properties.MessageId = request.RequestId;
                properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

                _channel.BasicPublish(
                    exchange: EXCHANGE_NAME,
                    routingKey: "image.processing",
                    basicProperties: properties,
                    body: body);

                _logger.LogInformation("Published image processing request: {RequestId}", request.RequestId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish image processing request: {RequestId}", request.RequestId);
                // Don't throw - allow fallback to synchronous processing
            }
        }

        public async Task PublishPredictionResultAsync(PredictionResult result)
        {
            try
            {
                if (_channel == null || !_channel.IsOpen)
                {
                    _logger.LogWarning("RabbitMQ channel not available, skipping publish");
                    return;
                }

                var json = JsonSerializer.Serialize(result, _jsonOptions);
                var body = Encoding.UTF8.GetBytes(json);

                var properties = _channel.CreateBasicProperties();
                properties.Persistent = true;
                properties.MessageId = result.Id.ToString();
                properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

                _channel.BasicPublish(
                    exchange: EXCHANGE_NAME,
                    routingKey: "prediction.result",
                    basicProperties: properties,
                    body: body);

                _logger.LogInformation("Published prediction result: {PredictionId}", result.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish prediction result: {PredictionId}", result.Id);
            }
        }

        public void StartConsuming()
        {
            try
            {
                if (_channel == null || !_channel.IsOpen)
                {
                    _logger.LogWarning("RabbitMQ channel not available, cannot start consuming");
                    return;
                }

                _consumer = new EventingBasicConsumer(_channel);
                _consumer.Received += async (model, ea) =>
                {
                    await ProcessImageAsync(ea);
                };

                _channel.BasicConsume(queue: IMAGE_PROCESSING_QUEUE, autoAck: false, consumer: _consumer);
                _logger.LogInformation("Started consuming from image processing queue");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start consuming");
            }
        }

        public void StopConsuming()
        {
            try
            {
                if (_channel?.IsOpen == true && _consumer != null)
                {
                    var consumerTag = _consumer.ConsumerTags.FirstOrDefault();
                    if (!string.IsNullOrEmpty(consumerTag))
                    {
                        _channel.BasicCancel(consumerTag);
                    }
                    _logger.LogInformation("Stopped consuming from image processing queue");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop consuming");
            }
        }

        public async Task<bool> IsHealthyAsync()
        {
            try
            {
                return _connection?.IsOpen == true && _channel?.IsOpen == true;
            }
            catch
            {
                return false;
            }
        }

        private async Task ProcessImageAsync(BasicDeliverEventArgs ea)
        {
            var deliveryTag = ea.DeliveryTag;

            try
            {
                var body = ea.Body.ToArray();
                var json = Encoding.UTF8.GetString(body);
                var request = JsonSerializer.Deserialize<ImageProcessingRequest>(json, _jsonOptions);

                if (request == null)
                {
                    _logger.LogWarning("Failed to deserialize image processing request");
                    _channel?.BasicNack(deliveryTag, false, false);
                    return;
                }

                _logger.LogInformation("Processing image request: {RequestId}", request.RequestId);

                // Create scope for dependency injection
                using var scope = _serviceProvider.CreateScope();
                var predictionService = scope.ServiceProvider.GetRequiredService<IPredictionService>();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // Get image from database
                var leafImage = await context.LeafImages
                    .Include(l => l.LeafImageSymptoms)
                    .FirstOrDefaultAsync(l => l.Id == request.LeafImageId);

                if (leafImage == null)
                {
                    _logger.LogWarning("LeafImage not found: {LeafImageId}", request.LeafImageId);
                    _channel?.BasicNack(deliveryTag, false, false);
                    return;
                }

                // Read image file
                var imagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot",
                    leafImage.FilePath.TrimStart('/'));

                if (!File.Exists(imagePath))
                {
                    _logger.LogWarning("Image file not found: {ImagePath}", imagePath);
                    _channel?.BasicNack(deliveryTag, false, false);
                    return;
                }

                var imageBytes = await File.ReadAllBytesAsync(imagePath);

                // Create prediction log
                var predictionLog = new PredictionLog
                {
                    LeafImageId = request.LeafImageId,
                    ModelType = "CNN",
                    RequestTime = request.RequestTime,
                    ApiStatus = "Processing",
                    ModelVersion = "v1.1",
                    RequestId = request.RequestId
                };

                context.PredictionLogs.Add(predictionLog);
                await context.SaveChangesAsync();

                // Perform prediction
                var startTime = DateTime.UtcNow;
                var result = await predictionService.PredictDiseaseAsync(imageBytes, leafImage.FilePath, request.SymptomIds);
                var processingTime = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;

                // Save prediction result
                var prediction = new Prediction
                {
                    LeafImageId = request.LeafImageId,
                    DiseaseName = result.DiseaseName,
                    Confidence = result.Confidence,
                    FinalConfidence = result.FinalConfidence,
                    ModelVersion = result.ModelVersion,
                    SeverityLevel = result.SeverityLevel,
                    TreatmentSuggestion = result.TreatmentSuggestion,
                    ProcessingTimeMs = processingTime
                };

                context.Predictions.Add(prediction);

                // Update prediction log
                predictionLog.ResponseTime = DateTime.UtcNow;
                predictionLog.ApiStatus = "Success";
                predictionLog.ProcessingTimeMs = processingTime;

                // Update image status
                leafImage.ImageStatus = "Processed";

                await context.SaveChangesAsync();

                // Set prediction ID
                result.Id = prediction.Id;

                // Publish result
                await PublishPredictionResultAsync(result);

                // Acknowledge message
                _channel?.BasicAck(deliveryTag, false);

                _logger.LogInformation("Successfully processed image request: {RequestId}, Prediction: {Disease} ({Confidence:P2})",
                    request.RequestId, result.DiseaseName, result.Confidence);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing image request");

                // Reject message (don't requeue to avoid infinite loops)
                _channel?.BasicNack(deliveryTag, false, false);

                // Update prediction log on error
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                    var requestJson = Encoding.UTF8.GetString(ea.Body.ToArray());
                    var request = JsonSerializer.Deserialize<ImageProcessingRequest>(requestJson, _jsonOptions);

                    if (request != null)
                    {
                        var predictionLog = await context.PredictionLogs
                            .FirstOrDefaultAsync(p => p.RequestId == request.RequestId);

                        if (predictionLog != null)
                        {
                            predictionLog.ApiStatus = "Failed";
                            predictionLog.ErrorMessage = ex.Message.Substring(0, Math.Min(500, ex.Message.Length));
                            predictionLog.ResponseTime = DateTime.UtcNow;
                            await context.SaveChangesAsync();
                        }
                    }
                }
                catch (Exception logEx)
                {
                    _logger.LogError(logEx, "Failed to update prediction log on error");
                }
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    try
                    {
                        StopConsuming();
                        _channel?.Close();
                        _connection?.Close();
                        _channel?.Dispose();
                        _connection?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error disposing MessageQueueService");
                    }
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