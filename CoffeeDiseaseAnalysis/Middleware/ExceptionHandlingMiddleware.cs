// ===================================================================
// 4. File: CoffeeDiseaseAnalysis/Middleware/ExceptionHandlingMiddleware.cs
// ===================================================================
using CoffeeDiseaseAnalysis.Models.DTOs;
using System.Net;
using System.Text.Json;

namespace CoffeeDiseaseAnalysis.Middleware
{
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred");
                await HandleExceptionAsync(context, ex);
            }
        }

        private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";

            var response = new ErrorResponse
            {
                ErrorCode = "INTERNAL_SERVER_ERROR",
                Message = "Đã xảy ra lỗi hệ thống",
                RequestId = context.TraceIdentifier
            };

            switch (exception)
            {
                case ArgumentNullException _:
                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    response.ErrorCode = "BAD_REQUEST";
                    response.Message = "Dữ liệu đầu vào không hợp lệ";
                    break;

                case UnauthorizedAccessException _:
                    context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    response.ErrorCode = "UNAUTHORIZED";
                    response.Message = "Không có quyền truy cập";
                    break;

                case FileNotFoundException _:
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    response.ErrorCode = "FILE_NOT_FOUND";
                    response.Message = "Không tìm thấy file";
                    break;

                default:
                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    break;
            }

            var jsonResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await context.Response.WriteAsync(jsonResponse);
        }
    }
}