using System.Globalization;

namespace QingFeng.Services;

public interface ILocalizationService
{
    /// <summary>
    /// Get localized string by key
    /// </summary>
    string GetString(string key);
    
    /// <summary>
    /// Get current culture
    /// </summary>
    CultureInfo GetCurrentCulture();
    
    /// <summary>
    /// Set current culture
    /// </summary>
    Task SetCultureAsync(string cultureName);
    
    /// <summary>
    /// Get available cultures
    /// </summary>
    List<CultureInfo> GetAvailableCultures();
    
    /// <summary>
    /// Event triggered when culture changes
    /// </summary>
    event EventHandler? CultureChanged;
}
