// File: CoffeeDiseaseAnalysis/Filters/ValidationFilter.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CoffeeDiseaseAnalysis.Filters
{
    /// <summary>
    /// Validation filter to handle model state validation
    /// </summary>
    public class ValidationFilter : IActionFilter
    {
        private readonly ILogger<ValidationFilter> _logger;

        public ValidationFilter(ILogger<ValidationFilter> logger)
        {
            _logger = logger;
        }

        public void OnActionExecuting(ActionExecutingContext context)
        {
            if (!context.ModelState.IsValid)
            {
                var errors = GetValidationErrors(context.ModelState);

                _logger.LogWarning(
                    "Model validation failed for {Action}. Errors: {Errors}",
                    context.ActionDescriptor.DisplayName,
                    string.Join("; ", errors.SelectMany(e => e.Value))
                );

                var response = new
                {
                    Success = false,
                    Message = "Dữ liệu không hợp lệ",
                    Errors = errors,
                    Timestamp = DateTime.UtcNow,
                    Action = context.ActionDescriptor.DisplayName
                };

                context.Result = new BadRequestObjectResult(response);
            }
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
            // No implementation needed for post-action
        }

        private Dictionary<string, List<string>> GetValidationErrors(ModelStateDictionary modelState)
        {
            var errors = new Dictionary<string, List<string>>();

            foreach (var kvp in modelState)
            {
                var fieldName = FormatFieldName(kvp.Key);
                var fieldErrors = new List<string>();

                foreach (var error in kvp.Value.Errors)
                {
                    var errorMessage = GetFriendlyErrorMessage(fieldName, error.ErrorMessage);
                    fieldErrors.Add(errorMessage);
                }

                if (fieldErrors.Any())
                {
                    errors[fieldName] = fieldErrors;
                }
            }

            return errors;
        }

        private string FormatFieldName(string fieldName)
        {
            if (string.IsNullOrEmpty(fieldName))
                return "General";

            // Convert PascalCase to friendly names
            var friendlyNames = new Dictionary<string, string>
            {
                { "Email", "Email" },
                { "Password", "Mật khẩu" },
                { "ConfirmPassword", "Xác nhận mật khẩu" },
                { "FullName", "Họ tên" },
                { "CurrentPassword", "Mật khẩu hiện tại" },
                { "NewPassword", "Mật khẩu mới" },
                { "ConfirmNewPassword", "Xác nhận mật khẩu mới" },
                { "File", "Tệp" },
                { "Files", "Tệp" },
                { "Image", "Hình ảnh" },
                { "Images", "Hình ảnh" }
            };

            return friendlyNames.TryGetValue(fieldName, out var friendlyName)
                ? friendlyName
                : fieldName;
        }

        private string GetFriendlyErrorMessage(string fieldName, string originalMessage)
        {
            // Common validation error translations
            var translations = new Dictionary<string, string>
            {
                { "is required", "là bắt buộc" },
                { "field is required", "là bắt buộc" },
                { "cannot be empty", "không được để trống" },
                { "must be at least", "phải có ít nhất" },
                { "must not exceed", "không được vượt quá" },
                { "is not a valid email", "không phải là email hợp lệ" },
                { "is not valid", "không hợp lệ" },
                { "does not match", "không khớp" },
                { "must contain", "phải chứa" },
                { "characters", "ký tự" },
                { "character", "ký tự" }
            };

            var message = originalMessage;

            // Apply translations
            foreach (var translation in translations)
            {
                message = message.Replace(translation.Key, translation.Value, StringComparison.OrdinalIgnoreCase);
            }

            // Handle specific field patterns
            if (message.Contains("Email"))
            {
                if (message.Contains("required"))
                    return "Email là bắt buộc";
                if (message.Contains("valid"))
                    return "Email không hợp lệ";
            }

            if (message.Contains("Password"))
            {
                if (message.Contains("required"))
                    return $"{fieldName} là bắt buộc";
                if (message.Contains("length"))
                    return $"{fieldName} phải có độ dài phù hợp";
                if (message.Contains("complexity"))
                    return $"{fieldName} không đáp ứng yêu cầu bảo mật";
            }

            // Handle file validation
            if (fieldName.Contains("Tệp") || fieldName.Contains("Hình ảnh"))
            {
                if (message.Contains("required"))
                    return $"{fieldName} là bắt buộc";
                if (message.Contains("size"))
                    return $"{fieldName} có kích thước không hợp lệ";
                if (message.Contains("type") || message.Contains("format"))
                    return $"{fieldName} có định dạng không được hỗ trợ";
            }

            // If no specific translation found, return formatted message
            return $"{fieldName} {message.ToLower()}";
        }
    }

    /// <summary>
    /// Extension methods for validation
    /// </summary>
    public static class ValidationExtensions
    {
        public static bool HasValidationErrors(this ActionContext context)
        {
            return !context.ModelState.IsValid;
        }

        public static List<string> GetAllValidationErrors(this ModelStateDictionary modelState)
        {
            return modelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .Where(msg => !string.IsNullOrEmpty(msg))
                .ToList();
        }
    }
}