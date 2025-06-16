// File: CoffeeDiseaseAnalysis/Models/DTOs/PredictionDTOs.cs - COMPLETE
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

    // DTO cho image processing request (RabbitMQ)
    public class ImageProcessingRequest
    {
        public int LeafImageId { get; set; }
        public string ImagePath { get; set; } = string.Empty;
        public List<int>? SymptomIds { get; set; }
        public string UserId { get; set; } = string.Empty;
        public DateTime RequestTime { get; set; }
        public string RequestId { get; set; } = string.Empty;
    }

    // DTO cho model management
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

    // DTO cho A/B testing
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

    // DTO cho dashboard statistics
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
    }

    public class PeriodDto
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int Days { get; set; }
    }

    // DTO cho health check
    public class HealthStatusDto
    {
        public string OverallStatus { get; set; } = string.Empty;
        public List<string> Issues { get; set; } = new();
        public ComponentHealthDto Components { get; set; } = new();
        public DateTime Timestamp { get; set; }
    }

    public class ComponentHealthDto
    {
        public ServiceHealthDto Database { get; set; } = new();
        public ServiceHealthDto AIModel { get; set; } = new();
        public ServiceHealthDto Cache { get; set; } = new();
        public ServiceHealthDto MessageQueue { get; set; } = new();
        public ServiceHealthDto Storage { get; set; } = new();
    }

    public class ServiceHealthDto
    {
        public bool IsHealthy { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Error { get; set; }
        public string? Details { get; set; }
        public DateTime LastCheck { get; set; }
    }
}