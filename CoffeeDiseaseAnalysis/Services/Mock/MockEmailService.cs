// File: CoffeeDiseaseAnalysis/Services/Mock/MockEmailService.cs
using CoffeeDiseaseAnalysis.Services.Interfaces;

namespace CoffeeDiseaseAnalysis.Services.Mock
{
    public class MockEmailService : IEmailService
    {
        private readonly ILogger<MockEmailService> _logger;

        public MockEmailService(ILogger<MockEmailService> logger)
        {
            _logger = logger;
        }

        public async Task SendEmailAsync(string to, string subject, string body)
        {
            await Task.Delay(150);
            _logger.LogInformation("Mock email sent to {To}: {Subject}", to, subject);
        }

        public async Task SendWelcomeEmailAsync(string to, string fullName)
        {
            await Task.Delay(150);
            _logger.LogInformation("Mock welcome email sent to {To} for {Name}", to, fullName);
        }

        public async Task SendPasswordResetEmailAsync(string to, string resetLink)
        {
            await Task.Delay(150);
            _logger.LogInformation("Mock password reset email sent to {To}", to);
        }

        public async Task<bool> IsHealthyAsync()
        {
            await Task.Delay(25);
            return true;
        }
    }
}