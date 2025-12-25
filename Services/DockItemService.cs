using Microsoft.EntityFrameworkCore;
using QingFeng.Data;
using QingFeng.Models;

namespace QingFeng.Services;

public class DockItemService : IDockItemService
{
    private readonly QingFengDbContext _context;
    private readonly ILogger<DockItemService> _logger;

    public DockItemService(QingFengDbContext context, ILogger<DockItemService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<DockItem>> GetAllDockItemsAsync()
    {
        try
        {
            return await _context.DockItems
                .OrderBy(d => d.SortOrder)
                .ThenBy(d => d.Title)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dock items");
            return new List<DockItem>();
        }
    }

    public async Task<DockItem?> GetDockItemAsync(string itemId)
    {
        try
        {
            return await _context.DockItems
                .FirstOrDefaultAsync(d => d.ItemId == itemId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dock item {ItemId}", itemId);
            return null;
        }
    }

    public async Task<DockItem> SaveDockItemAsync(DockItem item)
    {
        try
        {
            var existing = await _context.DockItems
                .FirstOrDefaultAsync(d => d.ItemId == item.ItemId);

            if (existing != null)
            {
                existing.Title = item.Title;
                existing.Icon = item.Icon;
                existing.Url = item.Url;
                existing.IconBackground = item.IconBackground;
                existing.IconColor = item.IconColor;
                existing.SortOrder = item.SortOrder;
                existing.AssociatedAppId = item.AssociatedAppId;
                existing.UpdatedAt = DateTime.UtcNow;

                _context.DockItems.Update(existing);
            }
            else
            {
                if (string.IsNullOrEmpty(item.ItemId))
                {
                    item.ItemId = Guid.NewGuid().ToString();
                }
                item.CreatedAt = DateTime.UtcNow;
                item.UpdatedAt = DateTime.UtcNow;
                _context.DockItems.Add(item);
            }

            await _context.SaveChangesAsync();
            return existing ?? item;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving dock item");
            throw;
        }
    }

    public async Task<bool> DeleteDockItemAsync(string itemId)
    {
        try
        {
            var item = await _context.DockItems
                .FirstOrDefaultAsync(d => d.ItemId == itemId);

            if (item == null)
                return false;

            // Prevent deletion of system items
            if (item.IsSystemItem)
            {
                _logger.LogWarning("Attempted to delete system dock item {ItemId}", itemId);
                return false;
            }

            _context.DockItems.Remove(item);
            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting dock item {ItemId}", itemId);
            return false;
        }
    }

    public async Task AddApplicationToDockAsync(Application application)
    {
        try
        {
            // Check if already exists
            var existing = await _context.DockItems
                .FirstOrDefaultAsync(d => d.AssociatedAppId == application.AppId);

            if (existing != null)
                return; // Already in dock

            // Get the max sort order and add 1
            var maxSortOrder = await _context.DockItems.MaxAsync(d => (int?)d.SortOrder) ?? 0;

            var dockItem = new DockItem
            {
                ItemId = Guid.NewGuid().ToString(),
                Title = application.Title,
                Icon = application.Icon,
                Url = application.Url,
                IconBackground = application.IconColor,
                IconColor = "white",
                SortOrder = maxSortOrder + 1,
                IsSystemItem = false,
                AssociatedAppId = application.AppId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.DockItems.Add(dockItem);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding application {AppId} to dock", application.AppId);
        }
    }

    public async Task RemoveDockItemByAppIdAsync(string appId)
    {
        try
        {
            var item = await _context.DockItems
                .FirstOrDefaultAsync(d => d.AssociatedAppId == appId);

            if (item != null && !item.IsSystemItem)
            {
                _context.DockItems.Remove(item);
                await _context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing dock item for app {AppId}", appId);
        }
    }

    public async Task InitializeDefaultDockItemsAsync()
    {
        try
        {
            var count = await _context.DockItems.CountAsync();
            if (count > 0)
                return; // Already initialized

            var defaultItems = new List<DockItem>
            {
                new() {
                    ItemId = "home",
                    Title = "首页",
                    Icon = "dashboard",
                    Url = "/",
                    IconBackground = "var(--dock-gradient-dashboard)",
                    IconColor = "rgb(55 65 81)",
                    SortOrder = 1,
                    IsSystemItem = true
                },
                new() {
                    ItemId = "system-monitor",
                    Title = "系统监控",
                    Icon = "terminal",
                    Url = "/system-monitor",
                    IconBackground = "var(--dock-gradient-terminal)",
                    IconColor = "white",
                    SortOrder = 2,
                    IsSystemItem = true
                },
                new() {
                    ItemId = "file-manager",
                    Title = "文件",
                    Icon = "folder_open",
                    Url = "/file-manager",
                    IconBackground = "var(--dock-gradient-files)",
                    IconColor = "white",
                    SortOrder = 3,
                    IsSystemItem = true
                },
                new() {
                    ItemId = "share-management",
                    Title = "共享管理",
                    Icon = "folder_shared",
                    Url = "/share-management",
                    IconBackground = "linear-gradient(135deg, #667eea 0%, #764ba2 100%)",
                    IconColor = "white",
                    SortOrder = 4,
                    IsSystemItem = true
                },
                new() {
                    ItemId = "settings",
                    Title = "设置",
                    Icon = "tune",
                    Url = "/settings",
                    IconBackground = "var(--dock-gradient-settings)",
                    IconColor = "rgb(55 65 81)",
                    SortOrder = 5,
                    IsSystemItem = true
                }
            };

            _context.DockItems.AddRange(defaultItems);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Initialized {Count} default dock items", defaultItems.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing default dock items");
        }
    }
}
