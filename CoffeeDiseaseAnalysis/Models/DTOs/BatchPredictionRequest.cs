// ===================================================================
// 4. File: CoffeeDiseaseAnalysis/Models/DTOs/BatchPredictionRequest.cs
// ===================================================================
using System.ComponentModel.DataAnnotations;

namespace CoffeeDiseaseAnalysis.Models.DTOs
{
    public class BatchPredictionRequest
    {
        [Required(ErrorMessage = "Vui lòng chọn ít nhất 1 ảnh")]
        [MaxLength(10, ErrorMessage = "Tối đa 10 ảnh mỗi batch")]
        public List<IFormFile> Images { get; set; } = new();

        [MaxLength(200)]
        public string? Description { get; set; }

        public bool UseMLPAnalysis { get; set; } = false;

        public List<int>? SymptomIds { get; set; } = new();
    }
}