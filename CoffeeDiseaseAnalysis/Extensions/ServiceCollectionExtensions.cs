// File: CoffeeDiseaseAnalysis/Extensions/ServiceCollectionExtensions.cs
using Microsoft.EntityFrameworkCore;
using CoffeeDiseaseAnalysis.Data;
using CoffeeDiseaseAnalysis.Services;
using CoffeeDiseaseAnalysis.Services.Interfaces;

namespace CoffeeDiseaseAnalysis.Extensions
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Add database context and related services
        /// </summary>
        public static IServiceCollection AddCoffeeDiseaseDatabase(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly("CoffeeDiseaseAnalysis");
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorNumbersToAdd: null);
                });

                // Enable sensitive data logging in development
                if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
                {
                    options.EnableSensitiveDataLogging();
                    options.EnableDetailedErrors();
                }
            });

            return services;
        }

        /// <summary>
        /// Add application services
        /// </summary>
        public static IServiceCollection AddCoffeeDiseaseServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Core AI Services
            services.AddScoped<IPredictionService, PredictionService>();
            services.AddScoped<IMLPService, MLPService>();
            services.AddScoped<IModelManagementService, ModelManagementService>();

            // Infrastructure Services
            services.AddScoped<ICacheService, CacheService>();
            services.AddScoped<IFileService, FileService>();
            services.AddScoped<IMessageQueueService, MessageQueueService>();

            // Business Services
            services.AddScoped<INotificationService, NotificationService>();
            services.AddScoped<IEmailService, EmailService>();
            services.AddScoped<IReportService, ReportService>();

            // Background Services (if needed)
            services.AddHostedService<ModelTrainingBackgroundService>();

            return services;
        }

        /// <summary>
        /// Add health checks for all dependencies
        /// </summary>
        public static IServiceCollection AddCoffeeDiseaseHealthChecks(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddHealthChecks()
                .AddDbContextCheck<ApplicationDbContext>("database",
                    tags: new[] { "critical", "database" })
                .AddCheck("redis", () =>
                {
                    try
                    {
                        // Simple Redis health check
                        var connectionString = configuration.GetConnectionString("Redis");
                        if (string.IsNullOrEmpty(connectionString))
                        {
                            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Degraded(
                                "Redis connection string not configured");
                        }
                        return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Redis is configured");
                    }
                    catch (Exception ex)
                    {
                        return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Degraded(
                            $"Redis health check failed: {ex.Message}");
                    }
                }, tags: new[] { "cache", "redis" })
                .AddCheck("storage", () =>
                {
                    try
                    {
                        var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                        var modelsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "models");

                        Directory.CreateDirectory(uploadsPath);
                        Directory.CreateDirectory(modelsPath);

                        // Check disk space
                        var driveInfo = new DriveInfo(Path.GetPathRoot(uploadsPath) ?? "C:");
                        var freeSpaceGB = driveInfo.AvailableFreeSpace / (1024L * 1024L * 1024L);

                        return freeSpaceGB > 1
                            ? Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(
                                $"Storage is healthy. Free space: {freeSpaceGB} GB")
                            : Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Degraded(
                                $"Low disk space: {freeSpaceGB} GB");
                    }
                    catch (Exception ex)
                    {
                        return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy(
                            $"Storage check failed: {ex.Message}");
                    }
                }, tags: new[] { "storage", "critical" })
                .AddCheck("memory", () =>
                {
                    var allocated = GC.GetTotalMemory(false);
                    var memoryMB = allocated / 1024 / 1024;
                    var threshold = 1024; // 1GB threshold

                    return memoryMB < threshold
                        ? Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(
                            $"Memory usage is normal: {memoryMB} MB")
                        : Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Degraded(
                            $"High memory usage: {memoryMB} MB");
                }, tags: new[] { "memory", "performance" });

            return services;
        }
    }
}