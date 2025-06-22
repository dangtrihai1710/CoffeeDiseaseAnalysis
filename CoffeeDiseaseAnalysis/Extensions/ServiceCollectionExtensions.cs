// ===================================================================
// 3. File: CoffeeDiseaseAnalysis/Extensions/ServiceCollectionExtensions.cs
// ===================================================================
using CoffeeDiseaseAnalysis.Models.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeDiseaseAnalysis.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddCustomApiValidation(this IServiceCollection services)
        {
            services.Configure<ApiBehaviorOptions>(options =>
            {
                options.InvalidModelStateResponseFactory = context =>
                {
                    var errors = context.ModelState
                        .Where(x => x.Value?.Errors?.Count > 0)
                        .SelectMany(x => x.Value!.Errors)
                        .Select(x => x.ErrorMessage)
                        .ToList();

                    var response = new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Dữ liệu đầu vào không hợp lệ",
                        Errors = errors,
                        StatusCode = 400
                    };

                    return new BadRequestObjectResult(response);
                };
            });

            return services;
        }

        public static IServiceCollection AddCustomCors(this IServiceCollection services)
        {
            services.AddCors(options =>
            {
                options.AddPolicy("Development", policy =>
                {
                    policy.WithOrigins("http://localhost:3000", "https://localhost:3000")
                          .AllowAnyMethod()
                          .AllowAnyHeader()
                          .AllowCredentials();
                });

                options.AddPolicy("Production", policy =>
                {
                    policy.WithOrigins("https://yourdomain.com")
                          .AllowAnyMethod()
                          .AllowAnyHeader()
                          .AllowCredentials();
                });
            });

            return services;
        }
    }
}