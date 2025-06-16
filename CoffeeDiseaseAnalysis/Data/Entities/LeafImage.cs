using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CoffeeDiseaseAnalysis.Data.Entities
{
    public class LeafImage
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(500)]
        public string FilePath { get; set; } = string.Empty;

        [Required]
        public DateTime UploadDate { get; set; } = DateTime.UtcNow;

        [Required]
        [StringLength(450)] // Identity UserId max length
        public string UserId { get; set; } = string.Empty;

        [Required]
        public long FileSize { get; set; } // Bytes

        [StringLength(50)]
        public string ImageStatus { get; set; } = "Pending"; // Pending, Processed, Failed

        [StringLength(32)]
        public string? ImageHash { get; set; } // MD5 hash cho caching

        [StringLength(10)]
        public string FileExtension { get; set; } = string.Empty; // .jpg, .png

        public int Width { get; set; }
        public int Height { get; set; }

        // Navigation properties
        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;

        public virtual ICollection<Prediction> Predictions { get; set; } = new List<Prediction>();
        public virtual ICollection<LeafImageSymptom> LeafImageSymptoms { get; set; } = new List<LeafImageSymptom>();
        public virtual ICollection<PredictionLog> PredictionLogs { get; set; } = new List<PredictionLog>();
        public virtual ICollection<TrainingData> TrainingDataRecords { get; set; } = new List<TrainingData>();
    }
}