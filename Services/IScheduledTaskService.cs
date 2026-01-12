using QingFeng.Models;

namespace QingFeng.Services;

public interface IScheduledTaskService
{
    /// <summary>
    /// Get all scheduled tasks
    /// </summary>
    Task<List<ScheduledTask>> GetAllTasksAsync();
    
    /// <summary>
    /// Get a specific task by ID
    /// </summary>
    Task<ScheduledTask?> GetTaskAsync(int id);
    
    /// <summary>
    /// Create a new scheduled task
    /// </summary>
    Task<ScheduledTask> CreateTaskAsync(ScheduledTask task);
    
    /// <summary>
    /// Update an existing task
    /// </summary>
    Task UpdateTaskAsync(ScheduledTask task);
    
    /// <summary>
    /// Delete a task
    /// </summary>
    Task DeleteTaskAsync(int id);
    
    /// <summary>
    /// Enable or disable a task
    /// </summary>
    Task SetTaskEnabledAsync(int id, bool enabled);
    
    /// <summary>
    /// Run a task immediately
    /// </summary>
    Task RunTaskNowAsync(int id);
    
    /// <summary>
    /// Stop a running task
    /// </summary>
    Task StopTaskAsync(int id);
    
    /// <summary>
    /// Get tasks that need to run
    /// </summary>
    Task<List<ScheduledTask>> GetPendingTasksAsync();
    
    /// <summary>
    /// Update task status and next run time
    /// </summary>
    Task UpdateTaskStatusAsync(int id, string status, DateTime? nextRunTime, string? error = null);
    
    /// <summary>
    /// Validate a cron expression
    /// </summary>
    bool ValidateCronExpression(string cronExpression);
    
    /// <summary>
    /// Calculate next run time from cron expression or interval
    /// </summary>
    DateTime? CalculateNextRunTime(ScheduledTask task);
}
