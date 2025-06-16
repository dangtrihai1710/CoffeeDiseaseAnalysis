using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CoffeeDiseaseAnalysis.Data.Entities
{
    public class PredictionLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int LeafImageId { get; set; }

        [Required]
        [StringLength(50)]
        public string ModelType { get; set; } = string.Empty; // CNN, MLP, Combined

        [Required]
        public DateTime RequestTime { get; set; } = DateTime.UtcNow;

        public DateTime? ResponseTime { get; set; }

        [Required]
        [StringLength(50)]
        public string ApiStatus { get; set; } = string.Empty; // Success, Failed, Timeout

        [StringLength(500)]
        public string? ErrorMessage { get; set; }

        [Required]
        [StringLength(50)]
        public string ModelVersion { get; set; } = string.Empty;

        [StringLength(100)]
        public string? RequestId { get; set; }

        public int ProcessingTimeMs { get; set; }

        [StringLength(50)]
        public string? ServerNode { get; set; }

        // Navigation properties
        [ForeignKey("LeafImageId")]
        public virtual LeafImage LeafImage { get; set; } = null!;
    }
}