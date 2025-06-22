//------------------------------------------------------------------------------------
// 6️⃣ FIX Prediction.cs - Thêm thuộc tính thiếu
//------------------------------------------------------------------------------------
// File: CoffeeDiseaseAnalysis/Data/Entities/Prediction.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CoffeeDiseaseAnalysis.Data.Entities
{
    public class Prediction
    {
        public int Id { get; set; }

        public int LeafImageId { get; set; }

        [MaxLength(50)]
        public string DiseaseName { get; set; } = string.Empty;

        [Column(TypeName = "decimal(5,4)")]
        public decimal Confidence { get; set; }

        public DateTime PredictionDate { get; set; } = DateTime.UtcNow;

        [MaxLength(1000)]
        public string? TreatmentSuggestion { get; set; }

        [MaxLength(20)]
        public string? SeverityLevel { get; set; } // Mild, Moderate, Severe

        // ✅ THÊM CÁC THUỘC TÍNH THIẾU CHO AI MODEL
        [MaxLength(100)]
        public string? RequestId { get; set; } // ✅ Link với PredictionLog

        [Column(TypeName = "decimal(5,4)")]
        public decimal? FinalConfidence { get; set; } // ✅ Confidence sau khi kết hợp MLP

        public int ProcessingTimeMs { get; set; } // ✅ Thời gian xử lý

        // Navigation properties
        public virtual LeafImage LeafImage { get; set; } = null!;
        public virtual ICollection<Feedback> Feedbacks { get; set; } = new List<Feedback>();
    }
}
