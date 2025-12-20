using System.Globalization;
using System.Resources;

namespace QingFeng.Services;

public class LocalizationService : ILocalizationService
{
    private readonly ISystemSettingService _settingService;
    private readonly ILogger<LocalizationService> _logger;
    private readonly ResourceManager _resourceManager;
    private CultureInfo _currentCulture;
    
    private static readonly List<CultureInfo> AvailableCultures = new()
    {
        new CultureInfo("zh-CN"),
        new CultureInfo("en-US")
    };

    public event EventHandler? CultureChanged;

    public LocalizationService(ISystemSettingService settingService, ILogger<LocalizationService> logger)
    {
        _settingService = settingService;
        _logger = logger;
        _resourceManager = new ResourceManager("QingFeng.Resources.Localizations", typeof(LocalizationService).Assembly);
        _currentCulture = new CultureInfo("zh-CN"); // Default culture
        
        // Load culture from settings asynchronously
        _ = InitializeCultureAsync();
    }

    private async Task InitializeCultureAsync()
    {
        try
        {
            var cultureName = await _settingService.GetSettingAsync("language");
            if (!string.IsNullOrEmpty(cultureName))
            {
                var culture = AvailableCultures.FirstOrDefault(c => c.Name == cultureName);
                if (culture != null)
                {
                    _currentCulture = culture;
                    CultureInfo.CurrentCulture = _currentCulture;
                    CultureInfo.CurrentUICulture = _currentCulture;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading culture from settings");
        }
    }

    public string GetString(string key)
    {
        try
        {
            var value = _resourceManager.GetString(key, _currentCulture);
            return value ?? key;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting localized string for key {Key}", key);
            return key;
        }
    }

    public CultureInfo GetCurrentCulture()
    {
        return _currentCulture;
    }

    public async Task SetCultureAsync(string cultureName)
    {
        try
        {
            var culture = AvailableCultures.FirstOrDefault(c => c.Name == cultureName);
            if (culture != null)
            {
                _currentCulture = culture;
                CultureInfo.CurrentCulture = _currentCulture;
                CultureInfo.CurrentUICulture = _currentCulture;
                
                // Save to settings
                await _settingService.SetSettingAsync("language", cultureName, "Appearance", "UI language");
                
                // Notify subscribers
                CultureChanged?.Invoke(this, EventArgs.Empty);
                
                _logger.LogInformation("Culture changed to {Culture}", cultureName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting culture to {Culture}", cultureName);
        }
    }

    public List<CultureInfo> GetAvailableCultures()
    {
        return AvailableCultures;
    }
}
