using Microsoft.AspNetCore.Components;
using System.Collections.Concurrent;

namespace QingFeng.Services;

public interface IDialogService
{
    Task<object?> ShowAsync<T>(string title, Dictionary<string, object>? parameters = null, DialogOptions? options = null) where T : ComponentBase;
    event Action? OnDialogsChanged;
    IReadOnlyList<DialogReference> GetDialogs();
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
    public Dictionary<string, object>? CachedParameters { get; set; }
    public DialogOptions? Options { get; set; }
    public TaskCompletionSource<object?> ResultCompletion { get; } = new();
    public bool IsOpen { get; set; } = true;
}

public class DialogService : IDialogService
{
    private readonly ILogger<DialogService> _logger;
    private readonly ConcurrentBag<DialogReference> _dialogs = new();
    private readonly object _eventLock = new();
    public event Action? OnDialogsChanged;

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

        _dialogs.Add(dialogReference);
        
        lock (_eventLock)
        {
            OnDialogsChanged?.Invoke();
        }
        
        return dialogReference.ResultCompletion.Task;
    }

    public IReadOnlyList<DialogReference> GetDialogs()
    {
        return _dialogs.Where(d => d.IsOpen).ToList().AsReadOnly();
    }

    public void Close(DialogReference dialog)
    {
        dialog.IsOpen = false;
        dialog.ResultCompletion.TrySetResult(null);
        
        lock (_eventLock)
        {
            OnDialogsChanged?.Invoke();
        }
    }
}
