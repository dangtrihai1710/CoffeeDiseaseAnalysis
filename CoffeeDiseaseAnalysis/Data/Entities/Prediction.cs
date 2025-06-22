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

        // Navigation properties
        public virtual LeafImage LeafImage { get; set; } = null!;
        public virtual ICollection<Feedback> Feedbacks { get; set; } = new List<Feedback>();
    }
}
