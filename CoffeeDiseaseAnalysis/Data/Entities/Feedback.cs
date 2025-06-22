// File: CoffeeDiseaseAnalysis/Data/Entities/Feedback.cs
using System.ComponentModel.DataAnnotations;

namespace CoffeeDiseaseAnalysis.Data.Entities
{
    public class Feedback
    {
        public int Id { get; set; }

        public int PredictionId { get; set; }

        [MaxLength(450)]
        public string UserId { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? FeedbackText { get; set; }

        public int Rating { get; set; } // 1-5

        public DateTime FeedbackDate { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual Prediction Prediction { get; set; } = null!;
        public virtual User User { get; set; } = null!;
    }
}