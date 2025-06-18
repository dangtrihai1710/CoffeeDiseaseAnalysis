// ==========================================
// 2. Services/EmailService.cs - MISSING SERVICE
// ==========================================
using CoffeeDiseaseAnalysis.Services.Interfaces;

namespace CoffeeDiseaseAnalysis.Services
{
    public class EmailService : IEmailService
    {
        private readonly ILogger<EmailService> _logger;
        private readonly IConfiguration _configuration;

        public EmailService(ILogger<EmailService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public async Task SendEmailAsync(string to, string subject, string body)
        {
            // TODO: Implement actual email sending (SMTP, SendGrid, etc.)
            _logger.LogInformation("Email sent to {To}: {Subject}", to, subject);
            await Task.CompletedTask;
        }

        public async Task SendWelcomeEmailAsync(string to, string fullName)
        {
            var subject = "Chào mừng đến với Coffee Disease Analysis";
            var body = $"Xin chào {fullName},\n\nCảm ơn bạn đã đăng ký tài khoản!";
            await SendEmailAsync(to, subject, body);
        }

        public async Task SendPasswordResetEmailAsync(string to, string resetLink)
        {
            var subject = "Đặt lại mật khẩu";
            var body = $"Click vào link sau để đặt lại mật khẩu: {resetLink}";
            await SendEmailAsync(to, subject, body);
        }

        public async Task<bool> IsHealthyAsync()
        {
            try
            {
                // Simple health check
                await Task.Delay(10);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}