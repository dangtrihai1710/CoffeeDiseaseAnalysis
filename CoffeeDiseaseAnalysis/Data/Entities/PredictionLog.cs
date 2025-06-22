//------------------------------------------------------------------------------------
// 5️⃣ FIX PredictionLog.cs - Thêm thuộc tính thiếu
//------------------------------------------------------------------------------------
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

        // ✅ THÊM CÁC THUỘC TÍNH THIẾU
        [MaxLength(50)]
        public string ModelVersion { get; set; } = "v1.0"; // ✅ Phiên bản model

        [MaxLength(100)]
        public string? RequestId { get; set; } // ✅ ID request để trace

        public DateTime RequestTime { get; set; } = DateTime.UtcNow;

        public DateTime? ResponseTime { get; set; }

        [MaxLength(20)]
        public string ApiStatus { get; set; } = "Success"; // Success, Failed, Timeout

        [MaxLength(500)]
        public string? ErrorMessage { get; set; }

        public int ProcessingTimeMs { get; set; } // ✅ Thời gian xử lý

        [MaxLength(50)]
        public string? ServerNode { get; set; } // ✅ Node server xử lý

        // Navigation properties
        public virtual LeafImage LeafImage { get; set; } = null!;
    }
}