//------------------------------------------------------------------------------------
// 3️⃣ FIX Symptom.cs - Thêm thuộc tính Weight
//------------------------------------------------------------------------------------
// File: CoffeeDiseaseAnalysis/Data/Entities/Symptom.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CoffeeDiseaseAnalysis.Data.Entities
{
    public class Symptom
    {
        public int Id { get; set; }

        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(100)]
        public string Category { get; set; } = "Leaf"; // Leaf, Stem, Root

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsActive { get; set; } = true;

        // ✅ THÊM THUỘC TÍNH WEIGHT
        [Column(TypeName = "decimal(3,2)")]
        public decimal Weight { get; set; } = 1.0m; // ✅ Trọng số cho ML model

        // Navigation properties
        public virtual ICollection<LeafImageSymptom> LeafImageSymptoms { get; set; } = new List<LeafImageSymptom>();
    }
}