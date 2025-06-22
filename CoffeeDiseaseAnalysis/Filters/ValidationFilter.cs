// ===================================================================
// 6. Fix: ValidationFilter compilation error
// File: CoffeeDiseaseAnalysis/Filters/ValidationFilter.cs - COMPLETE
// ===================================================================
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CoffeeDiseaseAnalysis.Filters
{
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
                    "Model validation failed for action {Action}. Errors: {Errors}",
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
                { "Images", "Hình ảnh" },
                { "Notes", "Ghi chú" },
                { "Description", "Mô tả" }
            };

            return friendlyNames.TryGetValue(fieldName, out var friendlyName)
                ? friendlyName
                : fieldName;
        }

        private string GetFriendlyErrorMessage(string fieldName, string errorMessage)
        {
            // Convert common validation messages to Vietnamese
            var messageMap = new Dictionary<string, string>
            {
                { "The field is required", $"{fieldName} là bắt buộc" },
                { "is required", $"{fieldName} là bắt buộc" },
                { "is not valid", $"{fieldName} không hợp lệ" },
                { "must be between", $"{fieldName} phải nằm trong khoảng" },
                { "cannot be null", $"{fieldName} không được để trống" }
            };

            foreach (var mapping in messageMap)
            {
                if (errorMessage.Contains(mapping.Key))
                {
                    return mapping.Value;
                }
            }

            return errorMessage; // Return original if no mapping found
        }
    }
}