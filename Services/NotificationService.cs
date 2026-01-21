using Microsoft.EntityFrameworkCore;
using QingFeng.Data;
using QingFeng.Models;

namespace QingFeng.Services;

public class NotificationService : INotificationService
{
    private readonly IDbContextFactory<QingFengDbContext> _dbFactory;
    private readonly ILogger<NotificationService> _logger;

    public event Func<Task>? OnNotificationChanged;

    public NotificationService(IDbContextFactory<QingFengDbContext> dbFactory, ILogger<NotificationService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task<Notification> CreateNotificationAsync(string title, string message, string type = "Info", string? actionUrl = null, string? icon = null)
    {
        try
        {
            await using var context = await _dbFactory.CreateDbContextAsync();
            
            var notification = new Notification
            {
                Title = title,
                Message = message,
                Type = type,
                ActionUrl = actionUrl,
                Icon = icon,
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            };

            context.Notifications.Add(notification);
            await context.SaveChangesAsync();

            _logger.LogInformation("Created notification: {Title}", title);
            
            // Notify subscribers
            await NotifyChangedAsync();
            
            return notification;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating notification");
            throw;
        }
    }

    public async Task<List<Notification>> GetAllNotificationsAsync()
    {
        try
        {
            await using var context = await _dbFactory.CreateDbContextAsync();
            return await context.Notifications
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all notifications");
            return new List<Notification>();
        }
    }

    public async Task<List<Notification>> GetUnreadNotificationsAsync()
    {
        try
        {
            await using var context = await _dbFactory.CreateDbContextAsync();
            return await context.Notifications
                .Where(n => !n.IsRead)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting unread notifications");
            return new List<Notification>();
        }
    }

    public async Task<int> GetUnreadCountAsync()
    {
        try
        {
            await using var context = await _dbFactory.CreateDbContextAsync();
            return await context.Notifications
                .Where(n => !n.IsRead)
                .CountAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting unread count");
            return 0;
        }
    }

    public async Task MarkAsReadAsync(int notificationId)
    {
        try
        {
            await using var context = await _dbFactory.CreateDbContextAsync();
            var notification = await context.Notifications.FindAsync(notificationId);
            
            if (notification != null)
            {
                notification.IsRead = true;
                await context.SaveChangesAsync();
                await NotifyChangedAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking notification as read");
        }
    }

    public async Task MarkAllAsReadAsync()
    {
        try
        {
            await using var context = await _dbFactory.CreateDbContextAsync();
            var unreadNotifications = await context.Notifications
                .Where(n => !n.IsRead)
                .ToListAsync();

            foreach (var notification in unreadNotifications)
            {
                notification.IsRead = true;
            }

            await context.SaveChangesAsync();
            await NotifyChangedAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking all notifications as read");
        }
    }

    public async Task DeleteNotificationAsync(int notificationId)
    {
        try
        {
            await using var context = await _dbFactory.CreateDbContextAsync();
            var notification = await context.Notifications.FindAsync(notificationId);
            
            if (notification != null)
            {
                context.Notifications.Remove(notification);
                await context.SaveChangesAsync();
                await NotifyChangedAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting notification");
        }
    }

    public async Task DeleteAllReadNotificationsAsync()
    {
        try
        {
            await using var context = await _dbFactory.CreateDbContextAsync();
            var readNotifications = await context.Notifications
                .Where(n => n.IsRead)
                .ToListAsync();

            context.Notifications.RemoveRange(readNotifications);
            await context.SaveChangesAsync();
            await NotifyChangedAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting read notifications");
        }
    }

    private async Task NotifyChangedAsync()
    {
        if (OnNotificationChanged != null)
        {
            await OnNotificationChanged.Invoke();
        }
    }
}
