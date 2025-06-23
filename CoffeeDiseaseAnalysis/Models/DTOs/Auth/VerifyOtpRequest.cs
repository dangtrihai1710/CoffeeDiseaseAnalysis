// ==========================================
// CoffeeDiseaseAnalysis/Models/DTOs/Auth/VerifyOtpRequest.cs - NEW DTO
// ==========================================
using System.ComponentModel.DataAnnotations;

namespace CoffeeDiseaseAnalysis.Models.DTOs.Auth
{
    public class VerifyOtpRequest
    {
        [Required(ErrorMessage = "Email là bắt buộc")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mã OTP là bắt buộc")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "Mã OTP phải có 6 số")]
        [RegularExpression(@"^\d{6}$", ErrorMessage = "Mã OTP chỉ chứa số")]
        public string OtpCode { get; set; } = string.Empty;
    }
}
