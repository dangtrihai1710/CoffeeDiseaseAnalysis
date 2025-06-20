// File: CoffeeDiseaseAnalysis/Models/DTOs/MLPPredictionResult.cs
namespace CoffeeDiseaseAnalysis.Models.DTOs
{
    public class MLPPredictionResult
    {
        public string DiseaseName { get; set; } = string.Empty;
        public decimal Confidence { get; set; }
        public Dictionary<string, decimal> AllClassProbabilities { get; set; } = new();
        public DateTime PredictionDate { get; set; } = DateTime.UtcNow;
        public int ProcessingTimeMs { get; set; }
        public string ModelVersion { get; set; } = "MLP_v1.0";
        public List<string> Features { get; set; } = new();
        public int TotalSymptoms { get; set; }
        public bool IsReliable { get; set; } = true;
    }
}