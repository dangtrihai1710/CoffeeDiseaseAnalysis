// File: CoffeeDiseaseAnalysis/Data/Entities/Symptom.cs
using System.ComponentModel.DataAnnotations;

namespace CoffeeDiseaseAnalysis.Data.Entities
{
    public class Symptom
    {
        public int Id { get; set; }

        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(50)]
        public string? Category { get; set; }

        // Navigation properties
        public virtual ICollection<LeafImageSymptom> LeafImageSymptoms { get; set; } = new List<LeafImageSymptom>();
    }
}