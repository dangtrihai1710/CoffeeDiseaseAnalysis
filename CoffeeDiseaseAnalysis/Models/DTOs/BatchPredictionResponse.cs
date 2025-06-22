namespace CoffeeDiseaseAnalysis.Models.DTOs
{
    // Ensure this is the intended definition of BatchPredictionResponse.  
    public class BatchPredictionResponse
    {
        public string BatchId { get; set; }
        public int TotalImages { get; set; }
        public int ProcessedImages { get; set; }
        public List<PredictionResult> Results { get; set; }
        public List<string> Errors { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string Status { get; set; }
        public int? TotalProcessingTimeMs { get; }
    }
}
