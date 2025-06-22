
//------------------------------------------------------------------------------------
// 4️⃣ CREATE ModelVersion.cs - Entity mới
//------------------------------------------------------------------------------------
// File: CoffeeDiseaseAnalysis/Data/Entities/ModelVersion.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CoffeeDiseaseAnalysis.Data.Entities
{
    public class ModelVersion
    {
        public int Id { get; set; }

        [MaxLength(100)]
        public string ModelName { get; set; } = string.Empty; // coffee_resnet50

        [MaxLength(20)]
        public string Version { get; set; } = string.Empty; // v1.0, v1.1

        [MaxLength(500)]
        public string FilePath { get; set; } = string.Empty; // /models/coffee_resnet50_v1.0.onnx

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column(TypeName = "decimal(5,4)")]
        public decimal Accuracy { get; set; } // 0.9200

        [Column(TypeName = "decimal(5,4)")]
        public decimal? ValidationAccuracy { get; set; }

        [Column(TypeName = "decimal(5,4)")]
        public decimal? TestAccuracy { get; set; }

        [MaxLength(100)]
        public string TrainingDatasetVersion { get; set; } = string.Empty;

        public int TrainingSamples { get; set; }
        public int ValidationSamples { get; set; }
        public int TestSamples { get; set; }

        [MaxLength(50)]
        public string ModelType { get; set; } = "CNN"; // CNN, MLP, Ensemble

        public long FileSizeBytes { get; set; }

        public bool IsActive { get; set; } = false;
        public bool IsProduction { get; set; } = false;

        [MaxLength(1000)]
        public string? Notes { get; set; }

        [MaxLength(450)]
        public string? CreatedByUserId { get; set; }

        // Navigation properties
        [ForeignKey("CreatedByUserId")]
        public virtual User? CreatedByUser { get; set; }
    }
}