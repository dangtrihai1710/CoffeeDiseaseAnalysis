// ===================================================================
// 10. File: CoffeeDiseaseAnalysis/Models/DTOs/ErrorResponse.cs
// ===================================================================
namespace CoffeeDiseaseAnalysis.Models.DTOs
{
    public class ErrorResponse
    {
        public string ErrorCode { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public List<string> Details { get; set; } = new();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string? RequestId { get; set; }
        public string? StackTrace { get; set; }
    }
}