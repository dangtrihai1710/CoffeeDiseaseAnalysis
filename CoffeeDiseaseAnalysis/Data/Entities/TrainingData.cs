using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CoffeeDiseaseAnalysis.Data.Entities
{
    public class TrainingData
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int LeafImageId { get; set; }

        [Required]
        [StringLength(100)]
        public string Label { get; set; } = string.Empty; // Cercospora, Healthy, Miner, Phoma, Rust

        [Required]
        [StringLength(50)]
        public string Source { get; set; } = string.Empty; // Original, Feedback, Manual, Augmented

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsValidated { get; set; } = false;

        [StringLength(50)]
        public string DatasetSplit { get; set; } = "train"; // train, val, test

        public bool IsUsedForTraining { get; set; } = false;

        [StringLength(100)]
        public string? OriginalPrediction { get; set; }

        [Column(TypeName = "decimal(5,4)")]
        public decimal? OriginalConfidence { get; set; }

        [StringLength(450)]
        public string? ValidatedByUserId { get; set; }

        public int? FeedbackId { get; set; }

        [StringLength(200)]
        public string? Notes { get; set; }

        [StringLength(50)]
        public string Quality { get; set; } = "Unknown"; // High, Medium, Low, Unknown

        // Navigation properties
        [ForeignKey("LeafImageId")]
        public virtual LeafImage LeafImage { get; set; } = null!;

        [ForeignKey("ValidatedByUserId")]
        public virtual User? ValidatedByUser { get; set; }

        [ForeignKey("FeedbackId")]
        public virtual Feedback? SourceFeedback { get; set; }
    }
}