using Microsoft.EntityFrameworkCore;
using QingFeng.Data;
using QingFeng.Models;
using System.Text.Json;

namespace QingFeng.Services;

public class SystemSettingService : ISystemSettingService
{
    private readonly IDbContextFactory<QingFengDbContext> _dbFactory;
    private readonly ILogger<SystemSettingService> _logger;

    public SystemSettingService(IDbContextFactory<QingFengDbContext> dbFactory, ILogger<SystemSettingService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task<string?> GetSettingAsync(string key)
    {
        try
        {
            await using var context = await _dbFactory.CreateDbContextAsync();
            var setting = await context.SystemSettings
                .FirstOrDefaultAsync(s => s.Key == key);

            return setting?.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting setting {Key}", key);
            return null;
        }
    }

    public async Task<T?> GetSettingAsync<T>(string key)
    {
        try
        {
            var value = await GetSettingAsync(key);
            if (string.IsNullOrEmpty(value))
                return default;

            if (typeof(T) == typeof(string))
                return (T)(object)value;

            return JsonSerializer.Deserialize<T>(value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting typed setting {Key}", key);
            return default;
        }
    }

    public async Task SetSettingAsync(string key, string value, string category = "", string description = "")
    {
        try
        {
            await using var context = await _dbFactory.CreateDbContextAsync();
            var existing = await context.SystemSettings
                .FirstOrDefaultAsync(s => s.Key == key);

            if (existing != null)
            {
                existing.Value = value;
                existing.UpdatedAt = DateTime.UtcNow;
                if (!string.IsNullOrEmpty(category))
                    existing.Category = category;
                if (!string.IsNullOrEmpty(description))
                    existing.Description = description;

                context.SystemSettings.Update(existing);
            }
            else
            {
                var newSetting = new SystemSetting
                {
                    Key = key,
                    Value = value,
                    Category = category,
                    Description = description,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                context.SystemSettings.Add(newSetting);
            }

            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting {Key}", key);
            throw;
        }
    }

    public async Task<List<SystemSetting>> GetAllSettingsAsync()
    {
        try
        {
            await using var context = await _dbFactory.CreateDbContextAsync();
            return await context.SystemSettings
                .OrderBy(s => s.Category)
                .ThenBy(s => s.Key)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all settings");
            return new List<SystemSetting>();
        }
    }

    public async Task<List<SystemSetting>> GetSettingsByCategoryAsync(string category)
    {
        try
        {
            await using var context = await _dbFactory.CreateDbContextAsync();
            return await context.SystemSettings
                .Where(s => s.Category == category)
                .OrderBy(s => s.Key)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting settings for category {Category}", category);
            return new List<SystemSetting>();
        }
    }

    public async Task InitializeDefaultSettingsAsync()
    {
        try
        {
            await using var context = await _dbFactory.CreateDbContextAsync();
            
            // Define all default settings
            var defaultSettings = new List<SystemSetting>
            {
                new() {
                    Key = "theme",
                    Value = "dark",
                    Category = "Appearance",
                    Description = "UI theme (dark/light)",
                    DataType = "string"
                },
                new() {
                    Key = "language",
                    Value = "zh-CN",
                    Category = "Appearance",
                    Description = "UI language",
                    DataType = "string"
                },
                new() {
                    Key = "welcomeMessage",
                    Value = "欢迎回来, 管理员",
                    Category = "Appearance",
                    Description = "Welcome message on home page",
                    DataType = "string"
                },
                new() {
                    Key = "enableWeather",
                    Value = "true",
                    Category = "Features",
                    Description = "Enable weather widget",
                    DataType = "bool"
                },
                new() {
                    Key = "weatherLocation",
                    Value = "Beijing",
                    Category = "Features",
                    Description = "Weather location",
                    DataType = "string"
                },
                new() {
                    Key = "refreshInterval",
                    Value = "5000",
                    Category = "System",
                    Description = "System monitor refresh interval (ms)",
                    DataType = "int"
                },
                new() {
                    Key = "enableNotifications",
                    Value = "true",
                    Category = "Features",
                    Description = "Enable system notifications",
                    DataType = "bool"
                },
                new() {
                    Key = "anydropStoragePath",
                    Value = Path.Combine(Directory.GetCurrentDirectory(), "AnydropFiles"),
                    Category = "System",
                    Description = "Anydrop attachment storage location",
                    DataType = "string"
                }
            };

            // Get all existing setting keys and use HashSet for O(1) lookup
            var existingKeys = await context.SystemSettings
                .Select(s => s.Key)
                .ToHashSetAsync();

            // Add only missing settings
            var missingSettings = defaultSettings
                .Where(ds => !existingKeys.Contains(ds.Key))
                .ToList();

            if (missingSettings.Any())
            {
                context.SystemSettings.AddRange(missingSettings);
                await context.SaveChangesAsync();
                _logger.LogInformation("Added {Count} missing default settings", missingSettings.Count);
            }
            else
            {
                _logger.LogDebug("All default settings already exist");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing default settings");
        }
    }
}
