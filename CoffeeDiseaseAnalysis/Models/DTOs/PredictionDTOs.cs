// File: CoffeeDiseaseAnalysis/Models/DTOs/PredictionDTOs.cs
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

    // Response DTOs
    public class PredictionResult
    {
        public int PredictionId { get; set; }
        public int LeafImageId { get; set; }
        public string DiseaseName { get; set; } = string.Empty;
        public decimal Confidence { get; set; }
        public string? SeverityLevel { get; set; }
        public string? TreatmentSuggestion { get; set; }
        public DateTime PredictionDate { get; set; }
        public string ImagePath { get; set; } = string.Empty;
        public List<SymptomInfo>? DetectedSymptoms { get; set; }
    }

    public class PredictionHistory
    {
        public int Id { get; set; }
        public string ImagePath { get; set; } = string.Empty;
        public string DiseaseName { get; set; } = string.Empty;
        public decimal Confidence { get; set; }
        public DateTime PredictionDate { get; set; }
        public string? SeverityLevel { get; set; }
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

    public class ProcessingStatus
    {
        public int LeafImageId { get; set; }
        public string Status { get; set; } = string.Empty; // Uploaded, Processing, Processed, Failed
        public DateTime LastUpdated { get; set; }
        public string? Message { get; set; }
        public PredictionResult? Result { get; set; }
    }
}