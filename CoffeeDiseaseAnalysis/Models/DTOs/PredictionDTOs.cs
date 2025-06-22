// ===================================================================
// File: CoffeeDiseaseAnalysis/Models/DTOs/PredictionDTOs.cs - UPDATED
// ===================================================================
public partial class PredictionResult
{
    public int Id { get; set; } // ✅ Để tương thích với database entity
    public int PredictionId { get; set; } // ✅ Để tương thích với DTO response
    public int LeafImageId { get; set; }
    public string DiseaseName { get; set; } = string.Empty;
    public decimal Confidence { get; set; }
    public string? SeverityLevel { get; set; }
    public string? TreatmentSuggestion { get; set; }
    public string? Description { get; set; }
    public DateTime PredictionDate { get; set; }
    public string ImagePath { get; set; } = string.Empty;
    public List<SymptomInfo>? DetectedSymptoms { get; set; }
    public int? ProcessingTimeMs { get; set; }

    // REAL AI specific fields
    public bool IsRealAI { get; set; } = true;
    public string ModelType { get; set; } = "ResNet50-ONNX";
    public string ModelVersion { get; set; } = "coffee_resnet50_model_final";

    // Additional fields for MessageQueue compatibility
    public decimal? FinalConfidence { get; set; } // Confidence sau khi kết hợp với MLP
    public string? RequestId { get; set; } // Link với request
    public string? ErrorMessage { get; set; }
    public string Status { get; set; } = "Success";
}

// ===================================================================
// SymptomInfo DTO
// ===================================================================
public class SymptomInfo
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Probability { get; set; }
    public string Category { get; set; } = string.Empty;
}
