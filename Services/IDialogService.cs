using Microsoft.AspNetCore.Components;

namespace QingFeng.Services;

public interface IDialogService
{
    Task<object?> ShowAsync<T>(string title, Dictionary<string, object>? parameters = null, DialogOptions? options = null) where T : ComponentBase;
}

public class DialogOptions
{
    public bool MaxWidth { get; set; }
    public bool FullWidth { get; set; }
    public bool CloseButton { get; set; } = true;
    public bool FullScreen { get; set; }
}

public class DialogService : IDialogService
{
    private readonly ILogger<DialogService> _logger;

    public DialogService(ILogger<DialogService> logger)
    {
        _logger = logger;
    }

    public Task<object?> ShowAsync<T>(string title, Dictionary<string, object>? parameters = null, DialogOptions? options = null) where T : ComponentBase
    {
        // This is a simplified implementation
        // In a real-world scenario, you would manage dialog state more robustly
        _logger.LogInformation("Dialog service called for component {ComponentType} with title {Title}", typeof(T).Name, title);
        return Task.FromResult<object?>(null);
    }
}
