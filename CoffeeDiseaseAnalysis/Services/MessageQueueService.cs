// File: CoffeeDiseaseAnalysis/Services/MessageQueueService.cs - FINAL FIX
using CoffeeDiseaseAnalysis.Data;
using CoffeeDiseaseAnalysis.Data.Entities;
using CoffeeDiseaseAnalysis.Models.DTOs;
using CoffeeDiseaseAnalysis.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
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

            // Không async trong constructor - sẽ init khi cần
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
                _logger.LogError(ex, "Failed to initialize RabbitMQ - continuing without message queue");
            }
        }

        public Task PublishImageProcessingRequestAsync(ImageProcessingRequest request)
        {
            try
            {
                EnsureInitialized();

                if (_channel == null || _channel.IsClosed)
                {
                    _logger.LogWarning("RabbitMQ channel not available, processing synchronously");
                    return Task.CompletedTask;
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
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish image processing request: {RequestId}", request.RequestId);
                return Task.CompletedTask;
            }
        }

        public Task PublishPredictionResultAsync(PredictionResult result)
        {
            try
            {
                EnsureInitialized();

                if (_channel == null || _channel.IsClosed)
                {
                    _logger.LogWarning("RabbitMQ channel not available, skipping result publish");
                    return Task.CompletedTask;
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
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish prediction result: {PredictionId}", result.Id);
                return Task.CompletedTask;
            }
        }

        public void StartConsuming()
        {
            try
            {
                EnsureInitialized();

                if (_channel == null || _channel.IsClosed)
                {
                    _logger.LogWarning("RabbitMQ channel not available, cannot start consuming");
                    return;
                }

                _consumer = new EventingBasicConsumer(_channel);
                _consumer.Received += (model, ea) =>
                {
                    _ = Task.Run(async () => await ProcessImageAsync(ea));
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

        public Task<bool> IsHealthyAsync()
        {
            try
            {
                var isHealthy = _connection?.IsOpen == true && _channel?.IsOpen == true;
                return Task.FromResult(isHealthy);
            }
            catch
            {
                return Task.FromResult(false);
            }
        }

        private void EnsureInitialized()
        {
            if (_connection == null || _channel == null)
            {
                InitializeRabbitMQ();
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
                    if (_channel != null && _channel.IsOpen)
                        _channel.BasicNack(deliveryTag, false, false);
                    return;
                }

                _logger.LogInformation("Processing image request: {RequestId}", request.RequestId);

                using var scope = _serviceProvider.CreateScope();
                var predictionService = scope.ServiceProvider.GetRequiredService<IPredictionService>();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var leafImage = await context.LeafImages
                    .Include(l => l.LeafImageSymptoms)
                    .FirstOrDefaultAsync(l => l.Id == request.LeafImageId);

                if (leafImage == null)
                {
                    _logger.LogWarning("LeafImage not found: {LeafImageId}", request.LeafImageId);
                    if (_channel != null && _channel.IsOpen)
                        _channel.BasicNack(deliveryTag, false, false);
                    return;
                }

                var imagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot",
                    leafImage.FilePath.TrimStart('/'));

                if (!File.Exists(imagePath))
                {
                    _logger.LogWarning("Image file not found: {ImagePath}", imagePath);
                    if (_channel != null && _channel.IsOpen)
                        _channel.BasicNack(deliveryTag, false, false);
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
                if (_channel != null && _channel.IsOpen)
                    _channel.BasicAck(deliveryTag, false);

                _logger.LogInformation("Successfully processed image request: {RequestId}, Prediction: {Disease} ({Confidence:P2})",
                    request.RequestId, result.DiseaseName, result.Confidence);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing image request");

                if (_channel != null && _channel.IsOpen)
                    _channel.BasicNack(deliveryTag, false, false);

                // Update prediction log on error
                await UpdatePredictionLogOnError(ea, ex);
            }
        }

        private async Task UpdatePredictionLogOnError(BasicDeliverEventArgs ea, Exception ex)
        {
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
                        predictionLog.ErrorMessage = ex.Message.Length > 500
                            ? ex.Message[..500]
                            : ex.Message;
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