// ==========================================
// File: CoffeeDiseaseAnalysis/Models/DTOs/ImageProcessingRequest.cs
// ==========================================

using System.ComponentModel.DataAnnotations;

namespace CoffeeDiseaseAnalysis.Models.DTOs
{
    public class ImageProcessingRequest
    {
        [Required]
        public string RequestId { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public int LeafImageId { get; set; }

        public List<int>? SymptomIds { get; set; } = new List<int>();

        public DateTime RequestTime { get; set; } = DateTime.UtcNow;

        [MaxLength(500)]
        public string? Notes { get; set; }

        [MaxLength(50)]
        public string ModelVersion { get; set; } = "v1.0";

        [MaxLength(450)]
        public string? UserId { get; set; }

        public bool IncludeSymptomAnalysis { get; set; } = false;

        // Metadata for processing
        public int Priority { get; set; } = 1; // 1=High, 5=Low
        public DateTime? ExpiresAt { get; set; }

        [MaxLength(100)]
        public string? Source { get; set; } = "WebAPI"; // WebAPI, Mobile, Batch
    }

    // ==========================================
    // Missing PredictionResult extension - đã có sẵn trong PredictionDTOs.cs
    // Nhưng cần thêm thuộc tính Id để match với code
    // ==========================================
    public partial class PredictionResult
    {
        public int Id { get; set; } // ✅ FIX: MessageQueueService.cs line 165 cần thuộc tính này

        public decimal? FinalConfidence { get; set; } // ✅ Confidence sau khi kết hợp với MLP

        [MaxLength(100)]
        public string? RequestId { get; set; } // ✅ Link với request

        // Thêm các thuộc tính để match với code trong MessageQueueService
        public string? ErrorMessage { get; set; }
        public string Status { get; set; } = "Success";
    }

    // ==========================================
    // ModelStatistics - đã được tham chiếu trong interface
    // ==========================================

    // ==========================================
    // Health Check DTOs
    // ==========================================
    public class HealthCheckResult
    {
        public bool IsHealthy { get; set; }
        public string Service { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CheckTime { get; set; } = DateTime.UtcNow;
        public string? ErrorMessage { get; set; }
        public Dictionary<string, object> Details { get; set; } = new Dictionary<string, object>();
    }

    // ==========================================
    // Cache DTOs
    // ==========================================
    public class CacheRequest
    {
        public string Key { get; set; } = string.Empty;
        public object Value { get; set; } = null!;
        public TimeSpan? Expiry { get; set; }
        public string? Tags { get; set; }
    }

    public class CacheResponse<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public DateTime? CacheTime { get; set; }
        public DateTime? ExpiryTime { get; set; }
        public string? Source { get; set; } = "Cache";
    }
}