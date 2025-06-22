// ===================================================================
// 7. File: CoffeeDiseaseAnalysis/Validators/BatchPredictionRequestValidator.cs
// ===================================================================
using CoffeeDiseaseAnalysis.Models.DTOs;
using FluentValidation;

namespace CoffeeDiseaseAnalysis.Validators
{
    public class BatchPredictionRequestValidator : AbstractValidator<BatchPredictionRequest>
    {
        public BatchPredictionRequestValidator()
        {
            RuleFor(x => x.Images)
                .NotNull()
                .NotEmpty()
                .WithMessage("Vui lòng chọn ít nhất 1 ảnh")
                .Must(x => x.Count <= 10)
                .WithMessage("Tối đa 10 ảnh mỗi batch");

            RuleFor(x => x.Description)
                .MaximumLength(200)
                .WithMessage("Mô tả không được vượt quá 200 ký tự");

            RuleForEach(x => x.Images)
                .Must(BeValidImageFile)
                .WithMessage("Tất cả file phải là ảnh hợp lệ");
        }

        private bool BeValidImageFile(IFormFile file)
        {
            if (file == null) return false;

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var maxSize = 10 * 1024 * 1024; // 10MB

            return allowedExtensions.Contains(extension) && file.Length <= maxSize;
        }
    }
}