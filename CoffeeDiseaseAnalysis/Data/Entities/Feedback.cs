using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CoffeeDiseaseAnalysis.Data.Entities
{
    public class Feedback
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int PredictionId { get; set; }

        [Required]
        [StringLength(450)] // Identity UserId max length
        public string UserId { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? FeedbackText { get; set; }

        [Required]
        [Range(1, 5)]
        public int Rating { get; set; }

        [Required]
        public DateTime FeedbackDate { get; set; } = DateTime.UtcNow;

        [StringLength(100)]
        public string? CorrectDiseaseName { get; set; }

        public bool IsUsedForTraining { get; set; } = false;

        [StringLength(50)]
        public string FeedbackType { get; set; } = "Manual"; // Manual, Auto, Expert

        // Navigation properties
        [ForeignKey("PredictionId")]
        public virtual Prediction Prediction { get; set; } = null!;

        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;
    }
}