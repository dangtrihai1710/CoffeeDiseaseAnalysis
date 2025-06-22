// File: CoffeeDiseaseAnalysis/Data/Entities/LeafImageSymptom.cs
using System.ComponentModel.DataAnnotations;

namespace CoffeeDiseaseAnalysis.Data.Entities
{
    public class LeafImageSymptom
    {
        public int LeafImageId { get; set; }
        public int SymptomId { get; set; }

        public DateTime ObservedDate { get; set; } = DateTime.UtcNow;

        [MaxLength(200)]
        public string? Notes { get; set; }

        // Navigation properties
        public virtual LeafImage LeafImage { get; set; } = null!;
        public virtual Symptom Symptom { get; set; } = null!;
    }
}