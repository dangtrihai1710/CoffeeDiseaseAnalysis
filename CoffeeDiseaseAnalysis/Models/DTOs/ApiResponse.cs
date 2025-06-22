// ===================================================================
// 7. File: CoffeeDiseaseAnalysis/Models/DTOs/ApiResponse.cs
// ===================================================================
namespace CoffeeDiseaseAnalysis.Models.DTOs
{
    public class ApiResponse<T>
    {
        public bool Success { get; set; } = true;
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }
        public List<string> Errors { get; set; } = new();
        public int StatusCode { get; set; } = 200;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class ApiResponse : ApiResponse<object>
    {
    }
}