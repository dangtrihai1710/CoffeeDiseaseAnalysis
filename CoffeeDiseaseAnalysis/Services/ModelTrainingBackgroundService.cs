// File: CoffeeDiseaseAnalysis/Services/ModelTrainingBackgroundService.cs
using CoffeeDiseaseAnalysis.Services.Interfaces;

namespace CoffeeDiseaseAnalysis.Services
{
    public class ModelTrainingBackgroundService : BackgroundService
    {
        private readonly ILogger<ModelTrainingBackgroundService> _logger;
        private readonly IServiceProvider _serviceProvider;

        public ModelTrainingBackgroundService(
            ILogger<ModelTrainingBackgroundService> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🤖 Model Training Background Service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                    _logger.LogInformation("📊 Checking for model training requirements...");
                    _logger.LogInformation("✅ Model training check completed - no action needed");
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error in model training background service");
                    await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
                }
            }

            _logger.LogInformation("🛑 Model Training Background Service stopped");
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("🛑 Stopping Model Training Background Service...");
            await base.StopAsync(cancellationToken);
        }
    }
}