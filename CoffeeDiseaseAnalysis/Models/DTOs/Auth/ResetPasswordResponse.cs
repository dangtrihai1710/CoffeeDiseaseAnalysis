// ==========================================
// CoffeeDiseaseAnalysis/Models/DTOs/Auth/ResetPasswordResponse.cs - NEW DTO
// ==========================================
namespace CoffeeDiseaseAnalysis.Models.DTOs.Auth
{
    public class ResetPasswordResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTime PasswordChangedAt { get; set; }
    }
}