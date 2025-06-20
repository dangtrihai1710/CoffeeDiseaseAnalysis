// File: CoffeeDiseaseAnalysis/Filters/ModelValidationActionFilter.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CoffeeDiseaseAnalysis.Filters
{
    public class ModelValidationActionFilter : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (!context.ModelState.IsValid)
            {
                var errors = context.ModelState
                    .Where(x => x.Value?.Errors.Count > 0)
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToList() ?? new List<string>()
                    );

                var response = new
                {
                    Success = false,
                    Message = "Dữ liệu không hợp lệ",
                    Errors = errors,
                    Timestamp = DateTime.UtcNow
                };

                context.Result = new BadRequestObjectResult(response);
            }
        }
    }
}