// ==========================================
// CoffeeDiseaseAnalysis/Models/DTOs/Auth/ForgotPasswordResponse.cs - NEW DTO
// ==========================================
namespace CoffeeDiseaseAnalysis.Models.DTOs.Auth
{
    public class ForgotPasswordResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTime OtpExpiresAt { get; set; }
        public int OtpExpiryMinutes { get; set; }
    }
}