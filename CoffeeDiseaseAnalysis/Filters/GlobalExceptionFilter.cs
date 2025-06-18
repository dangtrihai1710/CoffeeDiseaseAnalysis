// File: CoffeeDiseaseAnalysis/Filters/GlobalExceptionFilter.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Net;

namespace CoffeeDiseaseAnalysis.Filters
{
    /// <summary>
    /// Global exception filter to handle unhandled exceptions
    /// </summary>
    public class GlobalExceptionFilter : IExceptionFilter
    {
        private readonly ILogger<GlobalExceptionFilter> _logger;
        private readonly IWebHostEnvironment _environment;

        public GlobalExceptionFilter(
            ILogger<GlobalExceptionFilter> logger,
            IWebHostEnvironment environment)
        {
            _logger = logger;
            _environment = environment;
        }

        public void OnException(ExceptionContext context)
        {
            var exception = context.Exception;
            var request = context.HttpContext.Request;

            // Log the exception with context
            _logger.LogError(exception,
                "Unhandled exception occurred. Path: {Path}, Method: {Method}, User: {User}",
                request.Path,
                request.Method,
                context.HttpContext.User?.Identity?.Name ?? "Anonymous");

            // Determine response based on exception type
            var (statusCode, message, errors) = GetErrorResponse(exception);

            var response = new
            {
                Success = false,
                Message = message,
                Errors = errors,
                Timestamp = DateTime.UtcNow,
                Path = request.Path.ToString(),
                Method = request.Method,
                // Include stack trace only in development
                Detail = _environment.IsDevelopment() ? exception.ToString() : null
            };

            context.Result = new JsonResult(response)
            {
                StatusCode = (int)statusCode
            };

            context.ExceptionHandled = true;

            // Add custom headers for debugging
            context.HttpContext.Response.Headers.Add("X-Error-Type", exception.GetType().Name);

            if (_environment.IsDevelopment())
            {
                context.HttpContext.Response.Headers.Add("X-Error-Source", exception.Source ?? "Unknown");
            }
        }

        private (HttpStatusCode statusCode, string message, List<string> errors) GetErrorResponse(Exception exception)
        {
            return exception switch
            {
                ArgumentNullException argEx => (
                    HttpStatusCode.BadRequest,
                    "Thiếu tham số bắt buộc",
                    new List<string> { $"Tham số '{argEx.ParamName}' không được để trống" }
                ),

                ArgumentException argEx => (
                    HttpStatusCode.BadRequest,
                    "Tham số không hợp lệ",
                    new List<string> { argEx.Message }
                ),

                UnauthorizedAccessException => (
                    HttpStatusCode.Unauthorized,
                    "Không có quyền truy cập",
                    new List<string> { "Vui lòng đăng nhập để tiếp tục" }
                ),

                FileNotFoundException fileEx => (
                    HttpStatusCode.NotFound,
                    "Không tìm thấy tệp",
                    new List<string> { $"Tệp '{fileEx.FileName}' không tồn tại" }
                ),

                DirectoryNotFoundException => (
                    HttpStatusCode.NotFound,
                    "Không tìm thấy thư mục",
                    new List<string> { "Thư mục không tồn tại" }
                ),

                InvalidOperationException invalidOp => (
                    HttpStatusCode.BadRequest,
                    "Thao tác không hợp lệ",
                    new List<string> { invalidOp.Message }
                ),

                NotSupportedException notSupported => (
                    HttpStatusCode.BadRequest,
                    "Thao tác không được hỗ trợ",
                    new List<string> { notSupported.Message }
                ),

                TimeoutException => (
                    HttpStatusCode.RequestTimeout,
                    "Yêu cầu hết thời gian chờ",
                    new List<string> { "Vui lòng thử lại sau" }
                ),

                OutOfMemoryException => (
                    HttpStatusCode.InternalServerError,
                    "Hệ thống quá tải",
                    new List<string> { "Vui lòng thử lại sau ít phút" }
                ),

                TaskCanceledException => (
                    HttpStatusCode.RequestTimeout,
                    "Yêu cầu bị hủy",
                    new List<string> { "Yêu cầu đã bị hủy do hết thời gian chờ" }
                ),

                // Database related exceptions
                Microsoft.EntityFrameworkCore.DbUpdateException dbEx => (
                    HttpStatusCode.BadRequest,
                    "Lỗi cơ sở dữ liệu",
                    new List<string> { GetDatabaseErrorMessage(dbEx) }
                ),

                // Generic exceptions
                Exception ex when ex.Message.Contains("network") || ex.Message.Contains("connection") => (
                    HttpStatusCode.ServiceUnavailable,
                    "Lỗi kết nối",
                    new List<string> { "Không thể kết nối đến dịch vụ. Vui lòng thử lại sau" }
                ),

                _ => (
                    HttpStatusCode.InternalServerError,
                    "Có lỗi xảy ra trong hệ thống",
                    new List<string> { "Vui lòng thử lại sau hoặc liên hệ hỗ trợ" }
                )
            };
        }

        private string GetDatabaseErrorMessage(Microsoft.EntityFrameworkCore.DbUpdateException dbEx)
        {
            var innerException = dbEx.InnerException?.Message ?? dbEx.Message;

            if (innerException.Contains("UNIQUE constraint") || innerException.Contains("duplicate"))
            {
                return "Dữ liệu đã tồn tại trong hệ thống";
            }

            if (innerException.Contains("FOREIGN KEY constraint"))
            {
                return "Không thể thực hiện do ràng buộc dữ liệu";
            }

            if (innerException.Contains("timeout"))
            {
                return "Thao tác cơ sở dữ liệu hết thời gian chờ";
            }

            return _environment.IsDevelopment()
                ? $"Lỗi cơ sở dữ liệu: {innerException}"
                : "Lỗi cơ sở dữ liệu";
        }
    }
}