// ===================================================================
// 12. File: CoffeeDiseaseAnalysis/Models/DTOs/ValidationError.cs
// ===================================================================
namespace CoffeeDiseaseAnalysis.Models.DTOs
{
    public class ValidationError
    {
        public string Field { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public object? AttemptedValue { get; set; }
    }

    public class ValidationResult
    {
        public bool IsValid { get; set; } = true;
        public List<ValidationError> Errors { get; set; } = new();
    }
}