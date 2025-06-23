// ==========================================
// CoffeeDiseaseAnalysis/Models/DTOs/Auth/ForgotPasswordRequest.cs - NEW DTO
// ==========================================
using System.ComponentModel.DataAnnotations;

namespace CoffeeDiseaseAnalysis.Models.DTOs.Auth
{
    public class ForgotPasswordRequest
    {
        [Required(ErrorMessage = "Email là bắt buộc")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string Email { get; set; } = string.Empty;
    }
}