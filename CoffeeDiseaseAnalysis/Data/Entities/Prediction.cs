using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CoffeeDiseaseAnalysis.Data.Entities
{
    public class Prediction
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int LeafImageId { get; set; }

        [Required]
        [StringLength(100)]
        public string DiseaseName { get; set; } = string.Empty; // Cercospora, Healthy, Miner, Phoma, Rust

        [Required]
        [Range(0.0, 1.0)]
        [Column(TypeName = "decimal(5,4)")]
        public decimal Confidence { get; set; }

        [Required]
        public DateTime PredictionDate { get; set; } = DateTime.UtcNow;

        [Required]
        [StringLength(50)]
        public string ModelVersion { get; set; } = string.Empty; // v1.0, v1.1, etc.

        [StringLength(1000)]
        public string? TreatmentSuggestion { get; set; }

        [StringLength(50)]
        public string SeverityLevel { get; set; } = "Unknown"; // Nhẹ, Trung bình, Nặng, Unknown

        [Column(TypeName = "decimal(5,4)")]
        public decimal? FinalConfidence { get; set; } // Sau khi kết hợp với MLP

        public int ProcessingTimeMs { get; set; } // Thời gian xử lý

        // Navigation properties
        [ForeignKey("LeafImageId")]
        public virtual LeafImage LeafImage { get; set; } = null!;

        public virtual ICollection<Feedback> Feedbacks { get; set; } = new List<Feedback>();
    }
}