// File: CoffeeDiseaseAnalysis/Extensions/DatabaseServiceExtensions.cs
using Microsoft.EntityFrameworkCore;
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

            return services;
        }
    }
}