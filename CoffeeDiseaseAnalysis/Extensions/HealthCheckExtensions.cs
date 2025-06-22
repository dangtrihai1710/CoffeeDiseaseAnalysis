// ===================================================================
// 3. Fix: HealthChecksBuilder extension method missing
// File: CoffeeDiseaseAnalysis/Extensions/HealthCheckExtensions.cs - NEW
// ===================================================================
using CoffeeDiseaseAnalysis.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CoffeeDiseaseAnalysis.Extensions
{
    public static class HealthCheckExtensions
    {
        public static IServiceCollection AddDbContext<TContext>(
            this IHealthChecksBuilder builder)
            where TContext : class
        {
            // This method should be called on IServiceCollection, not IHealthChecksBuilder
            throw new InvalidOperationException(
                "AddDbContext should be called on IServiceCollection, not IHealthChecksBuilder. " +
                "Use builder.Services.AddDbContext<TContext>() instead."
            );
        }

        public static IHealthChecksBuilder AddApplicationDbContext(
            this IHealthChecksBuilder builder)
        {
            return builder.AddCheck<DatabaseHealthCheck>("database");
        }
    }

    // Custom health check implementation
    public class DatabaseHealthCheck : IHealthCheck
    {
        private readonly IServiceProvider _serviceProvider;

        public DatabaseHealthCheck(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // Simple database connectivity check
                await dbContext.Database.CanConnectAsync(cancellationToken);

                return HealthCheckResult.Healthy("Database connection successful");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("Database connection failed", ex);
            }
        }
    }
}
