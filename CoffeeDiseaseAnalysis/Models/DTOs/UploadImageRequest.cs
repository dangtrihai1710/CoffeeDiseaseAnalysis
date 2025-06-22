// ===================================================================
// 1. File: CoffeeDiseaseAnalysis/Models/DTOs/UploadImageRequest.cs
// ===================================================================
using System.ComponentModel.DataAnnotations;

namespace CoffeeDiseaseAnalysis.Models.DTOs
{
    public class UploadImageRequest
    {
        [Required(ErrorMessage = "Vui lòng chọn ảnh")]
        public IFormFile Image { get; set; } = null!;

        [MaxLength(100)]
        public string? Description { get; set; }

        public List<int>? SymptomIds { get; set; } = new();

        public bool UseMLPAnalysis { get; set; } = false;
    }
}