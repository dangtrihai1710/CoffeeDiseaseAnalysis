// File: CoffeeDiseaseAnalysis/Extensions/DatabaseServiceExtensions.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using CoffeeDiseaseAnalysis.Data;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class DatabaseServiceExtensions
    {
        public static IServiceCollection AddCoffeeDiseaseDatabase(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Thêm ApplicationDbContext
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseSqlServer(
                    configuration.GetConnectionString("DefaultConnection"),
                    sqlOptions =>
                    {
                        sqlOptions.CommandTimeout(30);
                        sqlOptions.EnableRetryOnFailure(
                            maxRetryCount: 3,
                            maxRetryDelay: TimeSpan.FromSeconds(5),
                            errorNumbersToAdd: null);
                    });

                // Chỉ enable sensitive data logging trong development
                if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
                {
                    options.EnableSensitiveDataLogging();
                    options.EnableDetailedErrors();
                }
            });

            // Thêm health check cho database
            services.AddScoped<DatabaseHealthCheck>();
            services.AddScoped<EntityFrameworkHealthCheck<ApplicationDbContext>>();

            return services;
        }

        // Thêm extension method cho Entity Framework Health Check
        public static IHealthChecksBuilder AddEntityFrameworkCheck<TContext>(
            this IHealthChecksBuilder builder,
            string? name = null,
            HealthStatus? failureStatus = null,
            IEnumerable<string>? tags = null,
            TimeSpan? timeout = null)
            where TContext : DbContext
        {
            return builder.AddCheck<EntityFrameworkHealthCheck<TContext>>(
                name ?? $"ef-{typeof(TContext).Name}",
                failureStatus,
                tags,
                timeout);
        }
    }

    // Health Check class riêng biệt
    public class EntityFrameworkHealthCheck<TContext> : IHealthCheck
        where TContext : DbContext
    {
        private readonly TContext _context;
        private readonly ILogger<EntityFrameworkHealthCheck<TContext>> _logger;

        public EntityFrameworkHealthCheck(TContext context, ILogger<EntityFrameworkHealthCheck<TContext>> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await _context.Database.CanConnectAsync(cancellationToken);
                _logger.LogInformation("Entity Framework {ContextName} health check passed", typeof(TContext).Name);
                return HealthCheckResult.Healthy($"Entity Framework {typeof(TContext).Name} is healthy");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Entity Framework {ContextName} health check failed", typeof(TContext).Name);
                return HealthCheckResult.Unhealthy($"Entity Framework {typeof(TContext).Name} failed", ex);
            }
        }
    }
}