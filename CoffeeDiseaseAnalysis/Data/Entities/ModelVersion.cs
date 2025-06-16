using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CoffeeDiseaseAnalysis.Data.Entities
{
    public class ModelVersion
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string ModelName { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string Version { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        public string FilePath { get; set; } = string.Empty;

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Required]
        [Column(TypeName = "decimal(5,4)")]
        public decimal Accuracy { get; set; }

        [Column(TypeName = "decimal(5,4)")]
        public decimal? ValidationAccuracy { get; set; }

        [Column(TypeName = "decimal(5,4)")]
        public decimal? TestAccuracy { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }

        public bool IsActive { get; set; } = false;

        public bool IsProduction { get; set; } = false;

        [Required]
        [StringLength(100)]
        public string TrainingDatasetVersion { get; set; } = string.Empty;

        public int TrainingSamples { get; set; }

        public int ValidationSamples { get; set; }

        public int TestSamples { get; set; }

        [StringLength(50)]
        public string ModelType { get; set; } = "CNN"; // CNN, MLP, Combined

        public long FileSizeBytes { get; set; }

        public DateTime? DeployedAt { get; set; }

        [StringLength(450)]
        public string? CreatedByUserId { get; set; }

        [StringLength(32)]
        public string? FileChecksum { get; set; }

        // Navigation properties
        [ForeignKey("CreatedByUserId")]
        public virtual User? CreatedByUser { get; set; }
    }
}