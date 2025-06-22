// File: CoffeeDiseaseAnalysis/Data/Entities/LeafImageSymptom.cs - FIXED VERSION
using System.ComponentModel.DataAnnotations;

namespace CoffeeDiseaseAnalysis.Data.Entities
{
    public class LeafImageSymptom
    {
        // ✅ THÊM Primary Key (vì migration có Id)
        public int Id { get; set; }

        // Composite key properties
        public int LeafImageId { get; set; }
        public int SymptomId { get; set; }

        public DateTime ObservedDate { get; set; } = DateTime.UtcNow;

        [MaxLength(200)]
        public string? Notes { get; set; }

        // ✅ THÊM CÁC PROPERTY THIẾU - khớp với migration
        [MaxLength(450)]
        public string? ObservedByUserId { get; set; } // ✅ User quan sát triệu chứng

        public int Intensity { get; set; } = 1; // ✅ Mức độ nghiêm trọng (1-5)

        // Navigation properties
        public virtual LeafImage LeafImage { get; set; } = null!;
        public virtual Symptom Symptom { get; set; } = null!;
        public virtual User? ObservedByUser { get; set; } // ✅ THÊM navigation property
    }
}