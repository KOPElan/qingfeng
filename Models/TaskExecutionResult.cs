namespace QingFeng.Models;

/// <summary>
/// Result object returned by scheduled task execution methods.
/// Contains structured information about task execution outcomes.
/// </summary>
public class TaskExecutionResult
{
    /// <summary>
    /// Indicates whether the task executed successfully
    /// </summary>
    public bool IsSuccess { get; set; }
    
    /// <summary>
    /// Human-readable result message describing what was accomplished
    /// </summary>
    public string ResultMessage { get; set; } = string.Empty;
    
    /// <summary>
    /// Error message if the task failed
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// Duration of the task execution in milliseconds
    /// </summary>
    public long? DurationMs { get; set; }
    
    /// <summary>
    /// Additional metadata about the task execution (e.g., file counts, statistics)
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
    
    /// <summary>
    /// Creates a successful result
    /// </summary>
    public static TaskExecutionResult Success(string message, Dictionary<string, object>? metadata = null, long? durationMs = null)
    {
        return new TaskExecutionResult
        {
            IsSuccess = true,
            ResultMessage = message,
            Metadata = metadata,
            DurationMs = durationMs
        };
    }
    
    /// <summary>
    /// Creates a failed result
    /// </summary>
    public static TaskExecutionResult Failure(string errorMessage, string? resultMessage = null, long? durationMs = null)
    {
        return new TaskExecutionResult
        {
            IsSuccess = false,
            ResultMessage = resultMessage ?? "任务执行失败",
            ErrorMessage = errorMessage,
            DurationMs = durationMs
        };
    }
}
