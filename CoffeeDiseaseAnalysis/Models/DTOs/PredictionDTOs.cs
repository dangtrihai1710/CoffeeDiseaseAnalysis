// File: CoffeeDiseaseAnalysis/Models/DTOs/PredictionDTOs.cs - UPDATED
using System.ComponentModel.DataAnnotations;

namespace CoffeeDiseaseAnalysis.Models.DTOs
{
    // Request DTOs
    public class UploadImageRequest
    {
        [Required(ErrorMessage = "Ảnh là bắt buộc")]
        public IFormFile Image { get; set; } = null!;

        public List<int>? SymptomIds { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public bool IncludeSymptomAnalysis { get; set; } = false;
    }

    public class FeedbackRequest
    {
        [Required]
        public int PredictionId { get; set; }

        [Range(1, 5)]
        public int Rating { get; set; }

        [MaxLength(1000)]
        public string? FeedbackText { get; set; }
    }

    public class BatchPredictionRequest
    {
        [Required]
        public List<IFormFile> Images { get; set; } = new List<IFormFile>();

        public List<int>? SymptomIds { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }
    }

    // Response DTOs
    public class PredictionResult
    {
        public int PredictionId { get; set; }
        public int LeafImageId { get; set; }
        public string DiseaseName { get; set; } = string.Empty;
        public decimal Confidence { get; set; }
        public string? SeverityLevel { get; set; }
        public string? TreatmentSuggestion { get; set; }
        public string? Description { get; set; }
        public DateTime PredictionDate { get; set; }
        public string ImagePath { get; set; } = string.Empty;
        public List<SymptomInfo>? DetectedSymptoms { get; set; }
        public int? ProcessingTimeMs { get; set; }

        // REAL AI specific fields
        public bool IsRealAI { get; set; } = true;
        public string ModelType { get; set; } = "ResNet50-ONNX";
        public string ModelVersion { get; set; } = "coffee_resnet50_model_final";
    }

    public class PredictionHistory
    {
        public int Id { get; set; }
        public string ImagePath { get; set; } = string.Empty;
        public string DiseaseName { get; set; } = string.Empty;
        public decimal Confidence { get; set; }
        public DateTime PredictionDate { get; set; }
        public string? SeverityLevel { get; set; }
        public string? TreatmentSuggestion { get; set; }
        public int? FeedbackRating { get; set; }
        public string? FeedbackText { get; set; }
    }

    public class SymptomInfo
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Category { get; set; }
    }

    public class BatchPredictionResponse
    {
        public string BatchId { get; set; } = string.Empty;
        public int TotalImages { get; set; }
        public int ProcessedImages { get; set; }
        public List<PredictionResult> Results { get; set; } = new List<PredictionResult>();
        public List<string> Errors { get; set; } = new List<string>();
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string Status { get; set; } = string.Empty;
        public int? TotalProcessingTimeMs => EndTime.HasValue ? (int)(EndTime.Value - StartTime).TotalMilliseconds : null;
    }

    public class ModelStatistics
    {
        public string ModelType { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public bool IsAvailable { get; set; }
        public int TotalPredictions { get; set; }
        public double AverageConfidence { get; set; }
        public Dictionary<string, int> DiseaseDistribution { get; set; } = new Dictionary<string, int>();
        public double AverageProcessingTime { get; set; }
        public double SuccessRate { get; set; }
        public DateTime LastUsed { get; set; }
    }
}
