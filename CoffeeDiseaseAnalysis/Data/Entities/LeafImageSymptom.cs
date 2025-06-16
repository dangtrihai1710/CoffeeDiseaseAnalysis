using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CoffeeDiseaseAnalysis.Data.Entities
{
    public class LeafImageSymptom
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int LeafImageId { get; set; }

        [Required]
        public int SymptomId { get; set; }

        [Required]
        public DateTime ObservedDate { get; set; } = DateTime.UtcNow;

        [Range(1, 5)]
        public int Intensity { get; set; } = 1; // 1-5: Nhẹ đến Nặng

        [StringLength(200)]
        public string? Notes { get; set; }

        [StringLength(450)]
        public string? ObservedByUserId { get; set; }

        // Navigation properties
        [ForeignKey("LeafImageId")]
        public virtual LeafImage LeafImage { get; set; } = null!;

        [ForeignKey("SymptomId")]
        public virtual Symptom Symptom { get; set; } = null!;

        [ForeignKey("ObservedByUserId")]
        public virtual User? ObservedByUser { get; set; }
    }
}