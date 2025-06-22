// ===================================================================
// 6. File: CoffeeDiseaseAnalysis/Validators/UploadImageRequestValidator.cs
// ===================================================================
using CoffeeDiseaseAnalysis.Models.DTOs;
using FluentValidation;

namespace CoffeeDiseaseAnalysis.Validators
{
    public class UploadImageRequestValidator : AbstractValidator<UploadImageRequest>
    {
        private readonly string[] _allowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
        private readonly int _maxFileSizeInMB = 10;

        public UploadImageRequestValidator()
        {
            RuleFor(x => x.Image)
                .NotNull()
                .WithMessage("Vui lòng chọn ảnh")
                .Must(BeValidImageFile)
                .WithMessage($"File phải là ảnh ({string.Join(", ", _allowedExtensions)}) và nhỏ hơn {_maxFileSizeInMB}MB");

            RuleFor(x => x.Description)
                .MaximumLength(100)
                .WithMessage("Mô tả không được vượt quá 100 ký tự");

            RuleFor(x => x.SymptomIds)
                .Must(x => x == null || x.Count <= 10)
                .WithMessage("Tối đa 10 triệu chứng");
        }

        private bool BeValidImageFile(IFormFile file)
        {
            if (file == null) return false;

            // Check file size
            if (file.Length > _maxFileSizeInMB * 1024 * 1024) return false;

            // Check extension
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            return _allowedExtensions.Contains(extension);
        }
    }
}