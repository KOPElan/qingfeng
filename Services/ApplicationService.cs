using Microsoft.EntityFrameworkCore;
using QingFeng.Data;
using QingFeng.Models;

namespace QingFeng.Services;

public class ApplicationService : IApplicationService
{
    private readonly IDbContextFactory<QingFengDbContext> _dbFactory;
    private readonly ILogger<ApplicationService> _logger;
    private readonly IDockItemService _dockItemService;

    public ApplicationService(
        IDbContextFactory<QingFengDbContext> dbFactory, 
        ILogger<ApplicationService> logger,
        IDockItemService dockItemService)
    {
        _dbFactory = dbFactory;
        _logger = logger;
        _dockItemService = dockItemService;
    }

    public async Task<List<Application>> GetAllApplicationsAsync()
    {
        try
        {
            await using var context = await _dbFactory.CreateDbContextAsync();
            return await context.Applications
                .OrderBy(a => a.SortOrder)
                .ThenBy(a => a.Title)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting applications");
            return new List<Application>();
        }
    }

    public async Task<Application?> GetApplicationAsync(string appId)
    {
        try
        {
            await using var context = await _dbFactory.CreateDbContextAsync();
            return await context.Applications
                .FirstOrDefaultAsync(a => a.AppId == appId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting application {AppId}", appId);
            return null;
        }
    }

    public async Task<Application> SaveApplicationAsync(Application application)
    {
        try
        {
            await using var context = await _dbFactory.CreateDbContextAsync();
            var existing = await context.Applications
                .FirstOrDefaultAsync(a => a.AppId == application.AppId);

            if (existing != null)
            {
                // Update existing
                existing.Title = application.Title;
                existing.Url = application.Url;
                existing.Icon = application.Icon;
                existing.IconColor = application.IconColor;
                existing.Description = application.Description;
                existing.Category = application.Category;
                existing.IsOnline = application.IsOnline;
                existing.SortOrder = application.SortOrder;
                existing.IsPinnedToDock = application.IsPinnedToDock;
                existing.UpdatedAt = DateTime.UtcNow;

                context.Applications.Update(existing);
            }
            else
            {
                // Add new
                if (string.IsNullOrEmpty(application.AppId))
                {
                    application.AppId = Guid.NewGuid().ToString();
                }
                application.CreatedAt = DateTime.UtcNow;
                application.UpdatedAt = DateTime.UtcNow;
                context.Applications.Add(application);
            }

            await context.SaveChangesAsync();
            return existing ?? application;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving application");
            throw;
        }
    }

    public async Task<bool> DeleteApplicationAsync(string appId)
    {
        try
        {
            await using var context = await _dbFactory.CreateDbContextAsync();
            var application = await context.Applications
                .FirstOrDefaultAsync(a => a.AppId == appId);

            if (application == null)
                return false;

            // Remove from dock if pinned
            if (application.IsPinnedToDock)
            {
                await _dockItemService.RemoveDockItemByAppIdAsync(appId);
            }

            context.Applications.Remove(application);
            await context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting application {AppId}", appId);
            return false;
        }
    }

    public async Task<bool> TogglePinToDockAsync(string appId)
    {
        try
        {
            await using var context = await _dbFactory.CreateDbContextAsync();
            var application = await context.Applications
                .FirstOrDefaultAsync(a => a.AppId == appId);

            if (application == null)
                return false;

            application.IsPinnedToDock = !application.IsPinnedToDock;
            application.UpdatedAt = DateTime.UtcNow;

            if (application.IsPinnedToDock)
            {
                // Add to dock
                await _dockItemService.AddApplicationToDockAsync(application);
            }
            else
            {
                // Remove from dock
                await _dockItemService.RemoveDockItemByAppIdAsync(appId);
            }

            context.Applications.Update(application);
            await context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling pin for application {AppId}", appId);
            return false;
        }
    }

    public async Task InitializeDefaultApplicationsAsync()
    {
        try
        {
            await using var context = await _dbFactory.CreateDbContextAsync();
            var count = await context.Applications.CountAsync();
            if (count > 0)
                return; // Already initialized

            var defaultApps = new List<Application>
            {
                new() { 
                    AppId = "terminal", 
                    Title = "终端", 
                    Url = "/terminal", 
                    Icon = "bi-terminal", 
                    IconColor = "#00ff00",
                    Description = "Web终端",
                    Category = "SYSTEM",
                    SortOrder = 0,
                    IsOnline = true
                },
                new() { 
                    AppId = "jellyfin", 
                    Title = "Jellyfin", 
                    Url = "http://localhost:8096", 
                    Icon = "bi-film", 
                    IconColor = "#aa5cc3",
                    Description = "媒体服务器",
                    Category = "MEDIA",
                    SortOrder = 1,
                    IsOnline = true
                },
                new() { 
                    AppId = "plex", 
                    Title = "Plex", 
                    Url = "http://localhost:32400", 
                    Icon = "bi-play-circle", 
                    IconColor = "#e5a00d",
                    Description = "媒体服务器",
                    Category = "MEDIA",
                    SortOrder = 2,
                    IsOnline = true
                },
                new() { 
                    AppId = "home-assistant", 
                    Title = "Home Asst", 
                    Url = "http://localhost:8123", 
                    Icon = "bi-house-heart", 
                    IconColor = "#41bdf5",
                    Description = "智能家居",
                    Category = "IOT",
                    SortOrder = 3,
                    IsOnline = true
                },
                new() { 
                    AppId = "pihole", 
                    Title = "Pi-hole", 
                    Url = "http://localhost:80/admin", 
                    Icon = "bi-shield-check", 
                    IconColor = "#f60d1a",
                    Description = "DNS服务",
                    Category = "DNS",
                    SortOrder = 4,
                    IsOnline = true
                },
                new() { 
                    AppId = "nextcloud", 
                    Title = "Nextcloud", 
                    Url = "http://localhost:80", 
                    Icon = "bi-cloud", 
                    IconColor = "#0082c9",
                    Description = "云存储",
                    Category = "FILES",
                    SortOrder = 5,
                    IsOnline = true
                },
                new() { 
                    AppId = "truenas", 
                    Title = "TrueNAS", 
                    Url = "http://localhost:80", 
                    Icon = "bi-database", 
                    IconColor = "#0095d5",
                    Description = "存储服务器",
                    Category = "NAS",
                    SortOrder = 6,
                    IsOnline = true
                },
                new() { 
                    AppId = "grafana", 
                    Title = "Grafana", 
                    Url = "http://localhost:3000", 
                    Icon = "bi-graph-up", 
                    IconColor = "#f46800",
                    Description = "数据可视化",
                    Category = "STATS",
                    SortOrder = 7,
                    IsOnline = true
                },
                new() { 
                    AppId = "portainer", 
                    Title = "Portainer", 
                    Url = "http://localhost:9000", 
                    Icon = "bi-box", 
                    IconColor = "#13bef9",
                    Description = "Docker管理",
                    Category = "DOCKER",
                    SortOrder = 8,
                    IsOnline = true
                },
                new() { 
                    AppId = "uptime-kuma", 
                    Title = "Uptime", 
                    Url = "http://localhost:3001", 
                    Icon = "bi-activity", 
                    IconColor = "#5cdd8b",
                    Description = "监控服务",
                    Category = "MONITOR",
                    SortOrder = 9,
                    IsOnline = true
                },
                new() { 
                    AppId = "transmission", 
                    Title = "Transmission", 
                    Url = "http://localhost:9091", 
                    Icon = "bi-download", 
                    IconColor = "#b8000b",
                    Description = "下载工具",
                    Category = "TORRENT",
                    SortOrder = 10,
                    IsOnline = true
                },
                new() { 
                    AppId = "vscode", 
                    Title = "VS Code", 
                    Url = "http://localhost:8080", 
                    Icon = "bi-code-slash", 
                    IconColor = "#007acc",
                    Description = "代码编辑器",
                    Category = "IDE",
                    SortOrder = 11,
                    IsOnline = true
                }
            };

            context.Applications.AddRange(defaultApps);
            await context.SaveChangesAsync();

            _logger.LogInformation("Initialized {Count} default applications", defaultApps.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing default applications");
        }
    }
}
