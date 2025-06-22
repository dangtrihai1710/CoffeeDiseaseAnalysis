// ===================================================================
// 2. Fix: ModelStatistics missing 'LastUsed' property
// File: CoffeeDiseaseAnalysis/Models/DTOs/ModelStatistics.cs - UPDATED
// ===================================================================
namespace CoffeeDiseaseAnalysis.Models.DTOs
{
    public class ModelStatistics
    {
        public string ModelType { get; set; } = "ResNet50";
        public string Version { get; set; } = "v1.0";
        public bool IsAvailable { get; set; } = true;
        public int TotalPredictions { get; set; }
        public double AverageConfidence { get; set; }
        public Dictionary<string, int> DiseaseDistribution { get; set; } = new();
        public int AverageProcessingTime { get; set; } // milliseconds
        public double SuccessRate { get; set; } = 1.0;
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        public string Status { get; set; } = "Active";

        // ✅ FIX: Add missing LastUsed property
        public DateTime? LastUsed { get; set; }

        // Additional statistics
        public int TodayPredictions { get; set; }
        public int FailedPredictions { get; set; }
        public double AverageAccuracy { get; set; } = 0.85;
        public string? LastError { get; set; }
        public DateTime? LastPredictionTime { get; set; }
    }
}