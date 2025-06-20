// File: CoffeeDiseaseAnalysis/Models/DTOs/PredictionDTOs.cs
using System.ComponentModel.DataAnnotations;

namespace CoffeeDiseaseAnalysis.Models.DTOs
{
    // REQUEST DTOs
    public class UploadImageRequest
    {
        [Required(ErrorMessage = "Ảnh là bắt buộc")]
        public IFormFile Image { get; set; } = null!;

        public List<int>? SymptomIds { get; set; }

        [StringLength(500, ErrorMessage = "Ghi chú không được vượt quá 500 ký tự")]
        public string? Notes { get; set; }
    }

    public class BatchPredictionRequest
    {
        [Required(ErrorMessage = "Danh sách ảnh là bắt buộc")]
        [MinLength(1, ErrorMessage = "Phải có ít nhất 1 ảnh")]
        public List<IFormFile> Images { get; set; } = new();

        public string? ModelVersion { get; set; }

        public bool IncludeSymptomAnalysis { get; set; } = true;
    }

    public class ImageProcessingRequest
    {
        public string LeafImageId { get; set; } = string.Empty;
        public string ImagePath { get; set; } = string.Empty;
        public List<int>? SymptomIds { get; set; }
        public string UserId { get; set; } = string.Empty;
        public DateTime RequestTime { get; set; }
        public string RequestId { get; set; } = string.Empty;
    }

    public class FeedbackRequest
    {
        [Required]
        public string PredictionId { get; set; } = string.Empty;

        [Required]
        [Range(1, 5)]
        public int Rating { get; set; }

        public string? FeedbackText { get; set; }

        public string? CorrectDiseaseName { get; set; }
    }

    public class UploadModelRequest
    {
        [Required]
        public IFormFile ModelFile { get; set; } = null!;

        [Required]
        public string ModelName { get; set; } = string.Empty;

        [Required]
        public string Version { get; set; } = string.Empty;

        public string ModelType { get; set; } = "CNN";

        [Range(0.0, 1.0)]
        public decimal Accuracy { get; set; }

        [Range(0.0, 1.0)]
        public decimal? ValidationAccuracy { get; set; }

        [Range(0.0, 1.0)]
        public decimal? TestAccuracy { get; set; }

        public string? TrainingDatasetVersion { get; set; }

        public int TrainingSamples { get; set; }
        public int ValidationSamples { get; set; }
        public int TestSamples { get; set; }
        public string? Notes { get; set; }
    }

    public class ABTestRequest
    {
        [Required]
        public int ModelAId { get; set; }

        [Required]
        public int ModelBId { get; set; }

        [Range(1, 99)]
        public int TrafficPercentageA { get; set; } = 50;

        [Range(1, 99)]
        public int TrafficPercentageB { get; set; } = 50;

        [Range(1, 30)]
        public int TestDurationDays { get; set; } = 7;
    }

    // RESPONSE DTOs
    public class PredictionResult
    {
        public string DiseaseName { get; set; } = string.Empty;
        public decimal Confidence { get; set; }
        public decimal? FinalConfidence { get; set; }
        public string SeverityLevel { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string TreatmentSuggestion { get; set; } = string.Empty;
        public string ModelVersion { get; set; } = string.Empty;
        public DateTime PredictionDate { get; set; }
        public int ProcessingTimeMs { get; set; }
        public string? ImagePath { get; set; }
        public List<SymptomDto>? DetectedSymptoms { get; set; }
    }

    public class BatchPredictionResponse
    {
        public List<PredictionResult> Results { get; set; } = new();
        public int TotalProcessed { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public List<string> Errors { get; set; } = new();
        public int TotalProcessingTimeMs { get; set; }
    }

    public class PredictionHistory
    {
        public string Id { get; set; } = string.Empty;
        public string ImagePath { get; set; } = string.Empty;
        public string DiseaseName { get; set; } = string.Empty;
        public decimal Confidence { get; set; }
        public string ModelVersion { get; set; } = string.Empty;
        public DateTime PredictionDate { get; set; }
        public string SeverityLevel { get; set; } = string.Empty;
        public decimal? FeedbackRating { get; set; }
        public string Status { get; set; } = string.Empty;
        public string ImageStatus { get; set; } = string.Empty;
    }

    public class FeedbackResponse
    {
        public string Id { get; set; } = string.Empty;
        public int Rating { get; set; }
        public string? FeedbackText { get; set; }
        public string? CorrectDiseaseName { get; set; }
        public DateTime FeedbackDate { get; set; }
        public bool IsUsedForTraining { get; set; }
    }

    // MODEL DTOs
    public class ModelStatistics
    {
        public string ModelVersion { get; set; } = string.Empty;
        public decimal AccuracyRate { get; set; }
        public int TotalPredictions { get; set; }
        public decimal AverageConfidence { get; set; }
        public int AverageProcessingTime { get; set; }
        public DateTime LastTrainingDate { get; set; }
        public string ModelSize { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public string ModelName { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public decimal Accuracy { get; set; }
        public decimal? ValidationAccuracy { get; set; }
        public decimal? TestAccuracy { get; set; }
        public bool IsProduction { get; set; }
        public decimal AverageRating { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? DeployedAt { get; set; }
    }

    public class ModelVersionInfo
    {
        public string Version { get; set; } = string.Empty;
        public decimal AccuracyRate { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }
        public string ModelSize { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    // SYMPTOM DTOs
    public class SymptomDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Category { get; set; } = string.Empty;
        public int? Intensity { get; set; }
        public decimal Weight { get; set; }
    }

    // SYSTEM DTOs
    public class SystemOverviewDto
    {
        public SystemStatsDto SystemStats { get; set; } = new();
        public CurrentModelDto CurrentModel { get; set; } = new();
        public PerformanceDto Performance { get; set; } = new();
        public PeriodDto Period { get; set; } = new();
    }

    public class SystemStatsDto
    {
        public int TotalUsers { get; set; }
        public int TotalImages { get; set; }
        public int TotalPredictions { get; set; }
        public int TotalFeedbacks { get; set; }
        public int RecentPredictions { get; set; }
        public int RecentImages { get; set; }
    }

    public class CurrentModelDto
    {
        public string ModelName { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public decimal Accuracy { get; set; }
        public int TotalPredictions { get; set; }
        public decimal AverageRating { get; set; }
    }

    public class PerformanceDto
    {
        public double ErrorRate { get; set; }
        public double AvgProcessingTime { get; set; }
        public List<DiseaseDistributionDto> DiseaseDistribution { get; set; } = new();
        public List<ProcessingStatusDto> ProcessingStatus { get; set; } = new();
    }

    public class DiseaseDistributionDto
    {
        public string Disease { get; set; } = string.Empty;
        public int Count { get; set; }
        public double AvgConfidence { get; set; }
        public double AvgProcessingTime { get; set; }
    }

    public class ProcessingStatusDto
    {
        public string Status { get; set; } = string.Empty;
        public int Count { get; set; }
        public double Percentage { get; set; }
    }

    public class PeriodDto
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Label { get; set; } = string.Empty;
    }
}