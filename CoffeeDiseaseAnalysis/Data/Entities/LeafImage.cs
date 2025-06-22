// File: CoffeeDiseaseAnalysis/Data/Entities/LeafImage.cs
using System.ComponentModel.DataAnnotations;

namespace CoffeeDiseaseAnalysis.Data.Entities
{
    public class LeafImage
    {
        public int Id { get; set; }

        [MaxLength(500)]
        public string FilePath { get; set; } = string.Empty;

        public DateTime UploadDate { get; set; } = DateTime.UtcNow;

        [MaxLength(450)]
        public string UserId { get; set; } = string.Empty;

        public long FileSize { get; set; }

        [MaxLength(20)]
        public string ImageStatus { get; set; } = "Uploaded"; // Uploaded, Processing, Processed, Failed

        [MaxLength(100)]
        public string? OriginalFileName { get; set; }

        // Navigation properties
        public virtual User User { get; set; } = null!;
        public virtual ICollection<Prediction> Predictions { get; set; } = new List<Prediction>();
        public virtual ICollection<LeafImageSymptom> LeafImageSymptoms { get; set; } = new List<LeafImageSymptom>();
        public virtual ICollection<PredictionLog> PredictionLogs { get; set; } = new List<PredictionLog>();
    }
}