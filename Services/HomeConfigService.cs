using Microsoft.EntityFrameworkCore;
using QingFeng.Data;
using QingFeng.Models;

namespace QingFeng.Services;

public class HomeConfigService : IHomeConfigService
{
    private readonly QingFengDbContext _context;
    private readonly ILogger<HomeConfigService> _logger;
    private bool _initialized = false;

    public HomeConfigService(QingFengDbContext context, ILogger<HomeConfigService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<ShortcutLink>> GetAllShortcutsAsync()
    {
        try
        {
            var items = await _context.Shortcuts
                .OrderByDescending(s => s.IsPinned)
                .ThenBy(s => s.SortOrder)
                .ThenBy(s => s.Category)
                .ThenBy(s => s.Title)
                .ToListAsync();

            return items.Select(i => i.ToShortcutLink()).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting shortcuts");
            return new List<ShortcutLink>();
        }
    }

    public async Task<List<ShortcutLink>> GetCustomShortcutsAsync()
    {
        try
        {
            var items = await _context.Shortcuts
                .Where(s => !s.Category.StartsWith("内置"))
                .OrderByDescending(s => s.IsPinned)
                .ThenBy(s => s.SortOrder)
                .ThenBy(s => s.Category)
                .ThenBy(s => s.Title)
                .ToListAsync();

            return items.Select(i => i.ToShortcutLink()).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting custom shortcuts");
            return new List<ShortcutLink>();
        }
    }

    public async Task<ShortcutLink?> GetShortcutAsync(string shortcutId)
    {
        try
        {
            var item = await _context.Shortcuts
                .FirstOrDefaultAsync(s => s.ShortcutId == shortcutId);

            return item?.ToShortcutLink();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting shortcut {ShortcutId}", shortcutId);
            return null;
        }
    }

    public async Task<ShortcutLink> SaveShortcutAsync(ShortcutLink shortcut)
    {
        try
        {
            var existing = await _context.Shortcuts
                .FirstOrDefaultAsync(s => s.ShortcutId == shortcut.Id);

            if (existing != null)
            {
                // Update existing
                existing.Title = shortcut.Title;
                existing.Url = shortcut.Url;
                existing.Icon = shortcut.Icon;
                existing.Description = shortcut.Description;
                existing.Category = shortcut.Category;
                existing.IsPinned = shortcut.IsPinned;
                existing.IsDocker = shortcut.IsDocker;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                // Add new
                var maxOrder = await _context.Shortcuts.MaxAsync(s => (int?)s.SortOrder) ?? 0;
                var item = ShortcutItem.FromShortcutLink(shortcut, maxOrder + 1);
                _context.Shortcuts.Add(item);
            }

            await _context.SaveChangesAsync();
            return shortcut;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving shortcut {ShortcutId}", shortcut.Id);
            throw;
        }
    }

    public async Task<bool> DeleteShortcutAsync(string shortcutId)
    {
        try
        {
            var item = await _context.Shortcuts
                .FirstOrDefaultAsync(s => s.ShortcutId == shortcutId);

            if (item == null)
                return false;

            _context.Shortcuts.Remove(item);
            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting shortcut {ShortcutId}", shortcutId);
            return false;
        }
    }

    public async Task<bool> TogglePinAsync(string shortcutId)
    {
        try
        {
            var item = await _context.Shortcuts
                .FirstOrDefaultAsync(s => s.ShortcutId == shortcutId);

            if (item == null)
                return false;

            item.IsPinned = !item.IsPinned;
            item.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling pin for shortcut {ShortcutId}", shortcutId);
            return false;
        }
    }

    public async Task<HashSet<string>> GetPinnedShortcutIdsAsync()
    {
        try
        {
            var pinnedIds = await _context.Shortcuts
                .Where(s => s.IsPinned)
                .Select(s => s.ShortcutId)
                .ToListAsync();

            return new HashSet<string>(pinnedIds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pinned shortcuts");
            return new HashSet<string>();
        }
    }

    public async Task<string?> GetConfigAsync(string key)
    {
        try
        {
            var config = await _context.HomeConfigs
                .FirstOrDefaultAsync(c => c.Key == key);

            return config?.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting config {Key}", key);
            return null;
        }
    }

    public async Task SetConfigAsync(string key, string value)
    {
        try
        {
            var config = await _context.HomeConfigs
                .FirstOrDefaultAsync(c => c.Key == key);

            if (config != null)
            {
                config.Value = value;
                config.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                config = new HomeLayoutConfig
                {
                    Key = key,
                    Value = value
                };
                _context.HomeConfigs.Add(config);
            }

            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting config {Key}", key);
            throw;
        }
    }

    public async Task InitializeDefaultsAsync()
    {
        if (_initialized)
            return;

        try
        {
            // Check if database exists and is accessible
            await _context.Database.EnsureCreatedAsync();

            // Check if shortcuts already exist
            var hasShortcuts = await _context.Shortcuts.AnyAsync();
            if (hasShortcuts)
            {
                _initialized = true;
                return;
            }

            // Add default shortcuts
            var defaultShortcuts = GetDefaultShortcuts();
            var sortOrder = 1;
            foreach (var shortcut in defaultShortcuts)
            {
                var item = ShortcutItem.FromShortcutLink(shortcut, sortOrder++);
                _context.Shortcuts.Add(item);
            }

            await _context.SaveChangesAsync();
            _initialized = true;
            _logger.LogInformation("Default shortcuts initialized");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing defaults");
        }
    }

    private static List<ShortcutLink> GetDefaultShortcuts()
    {
        return new List<ShortcutLink>
        {
            new ShortcutLink { Id = "internal-home", Title = "主页", Url = "/", Icon = "Icons.Material.Filled.Home", Description = "清风入口", Category = "内置", IsPinned = true },
            new ShortcutLink { Id = "internal-monitor", Title = "系统监控", Url = "/system-monitor", Icon = "Icons.Material.Filled.DashboardCustomize", Description = "CPU / 内存 / 磁盘", Category = "内置", IsPinned = true },
            new ShortcutLink { Id = "internal-docker", Title = "Docker 管理", Url = "/docker", Icon = "Icons.Material.Filled.Dns", Description = "容器与镜像", Category = "内置" },
            new ShortcutLink { Id = "internal-disk", Title = "磁盘管理", Url = "/disk-management", Icon = "Icons.Material.Filled.Storage", Description = "卷与存储", Category = "内置" },
            new ShortcutLink { Id = "internal-files", Title = "文件管理", Url = "/file-manager", Icon = "Icons.Material.Filled.Folder", Description = "浏览文件", Category = "内置" },
            new ShortcutLink { Id = "ha", Title = "Home Assistant", Url = "http://homeassistant.local:8123", Icon = "Icons.Material.Filled.Home", Description = "智能家居面板", Category = "自托管", IsPinned = true },
            new ShortcutLink { Id = "pihole", Title = "Pi-hole", Url = "http://pi.hole/admin", Icon = "Icons.Material.Filled.Security", Description = "网络广告拦截", Category = "自托管" },
            new ShortcutLink { Id = "plex", Title = "Plex", Url = "http://localhost:32400/web", Icon = "Icons.Material.Filled.Tv", Description = "媒体中心", Category = "影音" },
            new ShortcutLink { Id = "jellyfin", Title = "Jellyfin", Url = "http://localhost:8096", Icon = "Icons.Material.Filled.PlayCircleFilled", Description = "家庭影院", Category = "影音" },
            new ShortcutLink { Id = "transmission", Title = "Transmission", Url = "http://localhost:9091", Icon = "Icons.Material.Filled.SwapVert", Description = "下载任务", Category = "工具" },
            new ShortcutLink { Id = "adguard", Title = "AdGuard Home", Url = "http://localhost:3000", Icon = "Icons.Material.Filled.VerifiedUser", Description = "DNS 过滤", Category = "自托管" },
            new ShortcutLink { Id = "tailscale", Title = "Tailscale", Url = "https://login.tailscale.com/admin", Icon = "Icons.Material.Filled.VpnLock", Description = "内网穿透", Category = "网络" },
            new ShortcutLink { Id = "etherpad", Title = "Etherpad", Url = "http://localhost:9001", Icon = "Icons.Material.Filled.EditNote", Description = "协作笔记", Category = "协作" }
        };
    }
}
