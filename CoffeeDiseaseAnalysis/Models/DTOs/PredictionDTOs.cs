// File: CoffeeDiseaseAnalysis/Models/DTOs/PredictionDTOs.cs
using System.ComponentModel.DataAnnotations;

namespace CoffeeDiseaseAnalysis.Models.DTOs
{
    // DTO cho upload ảnh
    public class UploadImageRequest
    {
        [Required]
        public IFormFile Image { get; set; } = null!;

        public List<int>? SymptomIds { get; set; }

        public string? Notes { get; set; }
    }

    // DTO cho kết quả dự đoán
    public class PredictionResult
    {
        public int Id { get; set; }
        public string DiseaseName { get; set; } = string.Empty;
        public decimal Confidence { get; set; }
        public decimal? FinalConfidence { get; set; }
        public string ModelVersion { get; set; } = string.Empty;
        public string SeverityLevel { get; set; } = string.Empty;
        public string? TreatmentSuggestion { get; set; }
        public DateTime PredictionDate { get; set; }
        public int ProcessingTimeMs { get; set; }
        public List<SymptomDto>? DetectedSymptoms { get; set; }
    }

    // DTO cho triệu chứng
    public class SymptomDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Category { get; set; } = string.Empty;
        public int? Intensity { get; set; }
        public decimal Weight { get; set; }
    }

    // DTO cho phản hồi
    public class FeedbackRequest
    {
        [Required]
        public int PredictionId { get; set; }

        [Required]
        [Range(1, 5)]
        public int Rating { get; set; }

        public string? FeedbackText { get; set; }

        public string? CorrectDiseaseName { get; set; }
    }

    // DTO cho response của feedback
    public class FeedbackResponse
    {
        public int Id { get; set; }
        public int Rating { get; set; }
        public string? FeedbackText { get; set; }
        public string? CorrectDiseaseName { get; set; }
        public DateTime FeedbackDate { get; set; }
        public bool IsUsedForTraining { get; set; }
    }

    // DTO cho lịch sử dự đoán
    public class PredictionHistory
    {
        public int Id { get; set; }
        public string ImagePath { get; set; } = string.Empty;
        public string DiseaseName { get; set; } = string.Empty;
        public decimal Confidence { get; set; }
        public string ModelVersion { get; set; } = string.Empty;
        public DateTime PredictionDate { get; set; }
        public string SeverityLevel { get; set; } = string.Empty;
        public int? FeedbackRating { get; set; }
        public string ImageStatus { get; set; } = string.Empty;
    }

    // DTO cho thống kê mô hình
    public class ModelStatistics
    {
        public string ModelName { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public decimal Accuracy { get; set; }
        public decimal? ValidationAccuracy { get; set; }
        public decimal? TestAccuracy { get; set; }
        public bool IsActive { get; set; }
        public bool IsProduction { get; set; }
        public int TotalPredictions { get; set; }
        public decimal AverageConfidence { get; set; }
        public decimal AverageRating { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? DeployedAt { get; set; }
    }

    // DTO cho batch prediction
    public class BatchPredictionRequest
    {
        [Required]
        public List<IFormFile> Images { get; set; } = new();

        public string? ModelVersion { get; set; }

        public bool IncludeSymptomAnalysis { get; set; } = false;
    }

    // DTO cho batch prediction response
    public class BatchPredictionResponse
    {
        public List<PredictionResult> Results { get; set; } = new();
        public int TotalProcessed { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public int TotalProcessingTimeMs { get; set; }
        public List<string> Errors { get; set; } = new();
    }
}