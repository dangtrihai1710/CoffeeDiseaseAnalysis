// File: CoffeeDiseaseAnalysis/Data/DatabaseHealthCheck.cs
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;

namespace CoffeeDiseaseAnalysis.Data
{
    public class DatabaseHealthCheck : IHealthCheck
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DatabaseHealthCheck> _logger;

        public DatabaseHealthCheck(ApplicationDbContext context, ILogger<DatabaseHealthCheck> logger)
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
                // Kiểm tra kết nối database
                await _context.Database.CanConnectAsync(cancellationToken);

                // Kiểm tra bảng Users có tồn tại không
                var userCount = await _context.Users.CountAsync(cancellationToken);

                _logger.LogInformation("Database health check passed. User count: {UserCount}", userCount);

                return HealthCheckResult.Healthy($"Database is healthy. User count: {userCount}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database health check failed");
                return HealthCheckResult.Unhealthy("Database is unhealthy", ex);
            }
        }
    }
}