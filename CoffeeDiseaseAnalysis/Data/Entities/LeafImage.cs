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

        [MaxLength(50)]
        public string ImageStatus { get; set; } = "Pending"; // ✅ FIX MaxLength

        [MaxLength(100)]
        public string? OriginalFileName { get; set; }

        // ✅ THÊM CÁC THUỘC TÍNH THIẾU
        [MaxLength(32)]
        public string? ImageHash { get; set; } // ✅ MD5 hash của ảnh

        [MaxLength(10)]
        public string FileExtension { get; set; } = string.Empty; // ✅ .jpg, .png

        public int Width { get; set; } // ✅ Chiều rộng ảnh
        public int Height { get; set; } // ✅ Chiều cao ảnh

        // Navigation properties
        public virtual User User { get; set; } = null!;
        public virtual ICollection<Prediction> Predictions { get; set; } = new List<Prediction>();
        public virtual ICollection<LeafImageSymptom> LeafImageSymptoms { get; set; } = new List<LeafImageSymptom>();
        public virtual ICollection<PredictionLog> PredictionLogs { get; set; } = new List<PredictionLog>();
        public virtual ICollection<TrainingData> TrainingDataRecords { get; set; } = new List<TrainingData>(); // ✅ THÊM
    }
}