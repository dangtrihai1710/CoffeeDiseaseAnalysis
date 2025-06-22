// ===================================================================
// 11. File: CoffeeDiseaseAnalysis/Models/DTOs/HealthCheckResponse.cs
// ===================================================================
namespace CoffeeDiseaseAnalysis.Models.DTOs
{
    public class HealthCheckResponse
    {
        public string Status { get; set; } = "Healthy";
        public Dictionary<string, object> Checks { get; set; } = new();
        public TimeSpan TotalDuration { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}