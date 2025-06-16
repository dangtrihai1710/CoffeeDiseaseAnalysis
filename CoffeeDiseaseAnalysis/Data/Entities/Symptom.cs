using System.ComponentModel.DataAnnotations;

namespace CoffeeDiseaseAnalysis.Data.Entities
{
    public class Symptom
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        [StringLength(100)]
        public string Category { get; set; } = string.Empty; // Leaf, Stem, Root, General

        public bool IsActive { get; set; } = true;

        [Range(0.0, 1.0)]
        public decimal Weight { get; set; } = 1.0m; // Trọng số trong MLP

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual ICollection<LeafImageSymptom> LeafImageSymptoms { get; set; } = new List<LeafImageSymptom>();
    }
}