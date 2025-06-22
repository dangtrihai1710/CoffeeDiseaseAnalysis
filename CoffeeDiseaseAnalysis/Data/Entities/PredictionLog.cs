// File: CoffeeDiseaseAnalysis/Data/Entities/PredictionLog.cs
using System.ComponentModel.DataAnnotations;

namespace CoffeeDiseaseAnalysis.Data.Entities
{
    public class PredictionLog
    {
        public int Id { get; set; }

        public int LeafImageId { get; set; }

        [MaxLength(50)]
        public string ModelType { get; set; } = "ResNet50";

        public DateTime RequestTime { get; set; } = DateTime.UtcNow;

        public DateTime? ResponseTime { get; set; }

        [MaxLength(20)]
        public string ApiStatus { get; set; } = "Success"; // Success, Failed, Timeout

        [MaxLength(500)]
        public string? ErrorMessage { get; set; }

        public int? ProcessingTimeMs { get; set; }

        // Navigation properties
        public virtual LeafImage LeafImage { get; set; } = null!;
    }
}