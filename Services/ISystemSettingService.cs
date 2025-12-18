using QingFeng.Models;

namespace QingFeng.Services;

public interface ISystemSettingService
{
    Task<string?> GetSettingAsync(string key);
    Task<T?> GetSettingAsync<T>(string key);
    Task SetSettingAsync(string key, string value, string category = "", string description = "");
    Task<List<SystemSetting>> GetAllSettingsAsync();
    Task<List<SystemSetting>> GetSettingsByCategoryAsync(string category);
    Task InitializeDefaultSettingsAsync();
}
