// ===================================================================
// File: CoffeeDiseaseAnalysis/Models/DTOs/ImageProcessingRequest.cs - COMPLETE
// ===================================================================
using System.ComponentModel.DataAnnotations;

public class ImageProcessingRequest
{
    [Required]
    public string RequestId { get; set; } = Guid.NewGuid().ToString();

    [Required]
    public int LeafImageId { get; set; }

    public List<int>? SymptomIds { get; set; } = new List<int>();

    public DateTime RequestTime { get; set; } = DateTime.UtcNow;

    [MaxLength(500)]
    public string? Notes { get; set; }

    [MaxLength(50)]
    public string ModelVersion { get; set; } = "v1.0";

    [MaxLength(450)]
    public string? UserId { get; set; }

    public bool IncludeSymptomAnalysis { get; set; } = false;

    // Metadata for processing
    public int Priority { get; set; } = 1; // 1=High, 5=Low
    public DateTime? ExpiresAt { get; set; }

    [MaxLength(100)]
    public string? Source { get; set; } = "WebAPI"; // WebAPI, Mobile, Batch
}