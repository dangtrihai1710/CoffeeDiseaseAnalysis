// ==========================================
// 5. Services/Interfaces/INotificationService.cs - MISSING SERVICE
// ==========================================
namespace CoffeeDiseaseAnalysis.Services.Interfaces
{
    public interface INotificationService
    {
        Task SendNotificationAsync(string userId, string message, string type = "info");
        Task SendBulkNotificationAsync(List<string> userIds, string message, string type = "info");
        Task<List<object>> GetUserNotificationsAsync(string userId, int pageSize = 10);
        Task MarkAsReadAsync(int notificationId);
        Task<bool> IsHealthyAsync();
    }
}