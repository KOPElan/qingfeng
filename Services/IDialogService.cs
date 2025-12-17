using Microsoft.AspNetCore.Components;

namespace QingFeng.Services;

public interface IDialogService
{
    Task<object?> ShowAsync<T>(string title, Dictionary<string, object>? parameters = null, DialogOptions? options = null) where T : ComponentBase;
    event Action<DialogReference>? OnDialogInstanceAdded;
    void Close(DialogReference dialog);
}

public class DialogOptions
{
    public bool MaxWidth { get; set; }
    public bool FullWidth { get; set; }
    public bool CloseButton { get; set; } = true;
    public bool FullScreen { get; set; }
}

public class DialogReference
{
    public Guid Id { get; } = Guid.NewGuid();
    public Type ComponentType { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public Dictionary<string, object>? Parameters { get; set; }
    public DialogOptions? Options { get; set; }
    public TaskCompletionSource<object?> ResultCompletion { get; } = new();
    public bool IsOpen { get; set; } = true;
}

public class DialogService : IDialogService
{
    private readonly ILogger<DialogService> _logger;
    public event Action<DialogReference>? OnDialogInstanceAdded;

    public DialogService(ILogger<DialogService> logger)
    {
        _logger = logger;
    }

    public Task<object?> ShowAsync<T>(string title, Dictionary<string, object>? parameters = null, DialogOptions? options = null) where T : ComponentBase
    {
        _logger.LogInformation("Dialog service called for component {ComponentType} with title {Title}", typeof(T).Name, title);
        
        var dialogReference = new DialogReference
        {
            ComponentType = typeof(T),
            Title = title,
            Parameters = parameters,
            Options = options ?? new DialogOptions()
        };

        OnDialogInstanceAdded?.Invoke(dialogReference);
        
        return dialogReference.ResultCompletion.Task;
    }

    public void Close(DialogReference dialog)
    {
        dialog.IsOpen = false;
        dialog.ResultCompletion.TrySetResult(null);
    }
}
