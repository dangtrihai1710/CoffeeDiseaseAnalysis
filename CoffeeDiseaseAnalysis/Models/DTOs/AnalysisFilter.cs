// ===================================================================
// 8. File: CoffeeDiseaseAnalysis/Models/DTOs/AnalysisFilter.cs
// ===================================================================
namespace CoffeeDiseaseAnalysis.Models.DTOs
{
    public class AnalysisFilter
    {
        public string? Disease { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string? SeverityLevel { get; set; }
        public string? ModelType { get; set; }
        public decimal? MinConfidence { get; set; }
        public decimal? MaxConfidence { get; set; }
        public string? Status { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? SortBy { get; set; } = "PredictionDate";
        public string? SortDirection { get; set; } = "desc";
    }
}