using QingFeng.Models;

namespace QingFeng.Services;

public interface INotificationService
{
    Task<Notification> CreateNotificationAsync(string title, string message, string type = "Info", string? actionUrl = null, string? icon = null);
    Task<List<Notification>> GetAllNotificationsAsync();
    Task<List<Notification>> GetUnreadNotificationsAsync();
    Task<int> GetUnreadCountAsync();
    Task MarkAsReadAsync(int notificationId);
    Task MarkAllAsReadAsync();
    Task DeleteNotificationAsync(int notificationId);
    Task DeleteAllReadNotificationsAsync();
    
    event Func<Task>? OnNotificationChanged;
}
