// ==========================================
// CoffeeDiseaseAnalysis/Services/Interfaces/IEmailService.cs - UPDATED INTERFACE
// ==========================================
namespace CoffeeDiseaseAnalysis.Services.Interfaces
{
    public interface IEmailService
    {
        Task SendEmailAsync(string to, string subject, string body);
        Task SendWelcomeEmailAsync(string to, string fullName);
        Task SendPasswordResetEmailAsync(string to, string resetLink);

        // ✅ NEW METHODS
        Task SendOtpEmailAsync(string to, string otpCode, string fullName);
        Task SendPasswordChangeConfirmationAsync(string to, string fullName);

        Task<bool> IsHealthyAsync();
    }
}