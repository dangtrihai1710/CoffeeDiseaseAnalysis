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

                // Kiểm tra một vài bảng quan trọng
                var userCount = await _context.Users.CountAsync(cancellationToken);
                var imageCount = await _context.LeafImages.CountAsync(cancellationToken);

                _logger.LogInformation("Database health check passed. Users: {UserCount}, Images: {ImageCount}",
                    userCount, imageCount);

                return HealthCheckResult.Healthy($"Database is healthy. Users: {userCount}, Images: {imageCount}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database health check failed");
                return HealthCheckResult.Unhealthy("Database connection failed", ex);
            }
        }
    }
}