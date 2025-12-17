using QingFeng.Models;

namespace QingFeng.Services;

public interface IHomeConfigService
{
    /// <summary>
    /// Gets all shortcuts
    /// </summary>
    Task<List<ShortcutLink>> GetAllShortcutsAsync();

    /// <summary>
    /// Gets all custom (non-default) shortcuts
    /// </summary>
    Task<List<ShortcutLink>> GetCustomShortcutsAsync();

    /// <summary>
    /// Gets a shortcut by its ID
    /// </summary>
    Task<ShortcutLink?> GetShortcutAsync(string shortcutId);

    /// <summary>
    /// Adds or updates a shortcut
    /// </summary>
    Task<ShortcutLink> SaveShortcutAsync(ShortcutLink shortcut);

    /// <summary>
    /// Deletes a shortcut
    /// </summary>
    Task<bool> DeleteShortcutAsync(string shortcutId);

    /// <summary>
    /// Toggles the pinned state of a shortcut
    /// </summary>
    Task<bool> TogglePinAsync(string shortcutId);

    /// <summary>
    /// Gets all pinned shortcut IDs
    /// </summary>
    Task<HashSet<string>> GetPinnedShortcutIdsAsync();

    /// <summary>
    /// Gets a configuration value
    /// </summary>
    Task<string?> GetConfigAsync(string key);

    /// <summary>
    /// Sets a configuration value
    /// </summary>
    Task SetConfigAsync(string key, string value);

    /// <summary>
    /// Initializes the database with default shortcuts
    /// </summary>
    Task InitializeDefaultsAsync();
}
