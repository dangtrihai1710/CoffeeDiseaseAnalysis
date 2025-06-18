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
                    // Check for training requests every hour
                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);

                    using var scope = _serviceProvider.CreateScope();
                    // Add training logic here when needed

                    _logger.LogInformation("🔄 Model training check completed");
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("🛑 Model Training Background Service is stopping");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error in Model Training Background Service");
                }
            }
        }
    }
}