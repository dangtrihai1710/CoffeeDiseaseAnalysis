// ===================================================================
// 5. File: CoffeeDiseaseAnalysis/Models/DTOs/BatchPredictionResponse.cs - FIXED
// ===================================================================
namespace CoffeeDiseaseAnalysis.Models.DTOs
{
    public class BatchPredictionResponse
    {
        public string BatchId { get; set; } = Guid.NewGuid().ToString();
        public int TotalImages { get; set; }
        public int ProcessedImages { get; set; }
        public List<PredictionResult> Results { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public DateTime StartTime { get; set; } = DateTime.UtcNow;
        public DateTime? EndTime { get; set; }
        public string Status { get; set; } = "Processing";

        // Fix: Change from readonly to computed property
        public int? TotalProcessingTimeMs =>
            EndTime.HasValue ? (int)(EndTime.Value - StartTime).TotalMilliseconds : null;
    }
}