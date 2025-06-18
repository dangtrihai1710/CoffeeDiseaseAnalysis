// ==========================================
// 1. Services/Interfaces/IEmailService.cs - MISSING SERVICE
// ==========================================
namespace CoffeeDiseaseAnalysis.Services.Interfaces
{
    public interface IEmailService
    {
        Task SendEmailAsync(string to, string subject, string body);
        Task SendWelcomeEmailAsync(string to, string fullName);
        Task SendPasswordResetEmailAsync(string to, string resetLink);
        Task<bool> IsHealthyAsync();
    }
}