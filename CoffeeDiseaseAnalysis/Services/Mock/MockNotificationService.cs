// File: CoffeeDiseaseAnalysis/Services/Mock/MockNotificationService.cs
using CoffeeDiseaseAnalysis.Services.Interfaces;

namespace CoffeeDiseaseAnalysis.Services.Mock
{
    public class MockNotificationService : INotificationService
    {
        private readonly ILogger<MockNotificationService> _logger;

        public MockNotificationService(ILogger<MockNotificationService> logger)
        {
            _logger = logger;
        }

        public async Task SendNotificationAsync(string userId, string message, string type = "info")
        {
            await Task.Delay(100);
            _logger.LogInformation("Mock notification sent to {UserId}: [{Type}] {Message}", userId, type, message);
        }

        public async Task SendBulkNotificationAsync(List<string> userIds, string message, string type = "info")
        {
            await Task.Delay(200);
            _logger.LogInformation("Mock bulk notification sent to {Count} users: [{Type}] {Message}", userIds.Count, type, message);
        }

        public async Task<List<object>> GetUserNotificationsAsync(string userId, int pageSize = 10)
        {
            await Task.Delay(100);
            return new List<object>
            {
                new { Id = 1, Message = "Mock notification 1", Type = "info", CreatedAt = DateTime.UtcNow },
                new { Id = 2, Message = "Mock notification 2", Type = "warning", CreatedAt = DateTime.UtcNow.AddMinutes(-5) }
            };
        }

        public async Task MarkAsReadAsync(int notificationId)
        {
            await Task.Delay(50);
            _logger.LogInformation("Mock: Marked notification {Id} as read", notificationId);
        }

        public async Task<bool> IsHealthyAsync()
        {
            await Task.Delay(25);
            return true;
        }
    }
}