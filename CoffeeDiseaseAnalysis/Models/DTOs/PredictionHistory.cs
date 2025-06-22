// ===================================================================
// 2. File: CoffeeDiseaseAnalysis/Models/DTOs/PredictionHistory.cs
// ===================================================================
namespace CoffeeDiseaseAnalysis.Models.DTOs
{
    public class PredictionHistory
    {
        public int Id { get; set; }
        public int LeafImageId { get; set; }
        public string DiseaseName { get; set; } = string.Empty;
        public decimal Confidence { get; set; }
        public string? SeverityLevel { get; set; }
        public string? TreatmentSuggestion { get; set; }
        public DateTime PredictionDate { get; set; }
        public string ImagePath { get; set; } = string.Empty;
        public string ModelType { get; set; } = "ResNet50";
        public string ModelVersion { get; set; } = "v1.0";
        public int ProcessingTimeMs { get; set; }
        public string Status { get; set; } = "Success";
        public string? UserName { get; set; }
        public string? Description { get; set; }
        public object FeedbackRating { get; internal set; }
        public string? FeedbackText { get; internal set; }
    }
}