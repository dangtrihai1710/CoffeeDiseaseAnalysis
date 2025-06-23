// ==========================================
// CoffeeDiseaseAnalysis/Models/DTOs/Auth/VerifyOtpResponse.cs - NEW DTO
// ==========================================
namespace CoffeeDiseaseAnalysis.Models.DTOs.Auth
{
    public class VerifyOtpResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool IsOtpValid { get; set; }
        public int AttemptsRemaining { get; set; }
        public TimeSpan? TimeRemaining { get; set; }
    }
}