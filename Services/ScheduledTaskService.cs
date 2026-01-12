using QingFeng.Models;
using QingFeng.Data;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Cronos;

namespace QingFeng.Services;

public class ScheduledTaskService : IScheduledTaskService
{
    private readonly ILogger<ScheduledTaskService> _logger;
    private readonly IDbContextFactory<QingFengDbContext> _dbContextFactory;

    public ScheduledTaskService(ILogger<ScheduledTaskService> logger, IDbContextFactory<QingFengDbContext> dbContextFactory)
    {
        _logger = logger;
        _dbContextFactory = dbContextFactory;
    }

    public async Task<List<ScheduledTask>> GetAllTasksAsync()
    {
        using var context = await _dbContextFactory.CreateDbContextAsync();
        return await context.ScheduledTasks
            .OrderBy(t => t.Name)
            .ToListAsync();
    }

    public async Task<ScheduledTask?> GetTaskAsync(int id)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync();
        return await context.ScheduledTasks.FindAsync(id);
    }

    public async Task<ScheduledTask> CreateTaskAsync(ScheduledTask task)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync();
        
        task.CreatedAt = DateTime.UtcNow;
        task.UpdatedAt = DateTime.UtcNow;
        task.Status = "Idle";
        
        // Calculate first run time if enabled
        if (task.IsEnabled)
        {
            task.NextRunTime = CalculateNextRunTime(task);
        }
        
        context.ScheduledTasks.Add(task);
        await context.SaveChangesAsync();
        
        _logger.LogInformation("创建定时任务: {TaskName} ({TaskType})", task.Name, task.TaskType);
        
        return task;
    }

    public async Task UpdateTaskAsync(ScheduledTask task)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync();
        
        var existing = await context.ScheduledTasks.FindAsync(task.Id);
        if (existing == null)
            throw new InvalidOperationException($"任务不存在: {task.Id}");
        
        existing.Name = task.Name;
        existing.Description = task.Description;
        existing.TaskType = task.TaskType;
        existing.Configuration = task.Configuration;
        existing.IsEnabled = task.IsEnabled;
        existing.IntervalMinutes = task.IntervalMinutes;
        existing.CronExpression = task.CronExpression;
        existing.IsOneTime = task.IsOneTime;
        existing.UpdatedAt = DateTime.UtcNow;
        
        // Recalculate next run time
        if (task.IsEnabled)
        {
            existing.NextRunTime = CalculateNextRunTime(existing);
        }
        else
        {
            existing.NextRunTime = null;
        }
        
        await context.SaveChangesAsync();
        
        _logger.LogInformation("更新定时任务: {TaskName}", task.Name);
    }

    public async Task DeleteTaskAsync(int id)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync();
        
        var task = await context.ScheduledTasks.FindAsync(id);
        if (task == null)
            throw new InvalidOperationException($"任务不存在: {id}");
        
        context.ScheduledTasks.Remove(task);
        await context.SaveChangesAsync();
        
        _logger.LogInformation("删除定时任务: {TaskName}", task.Name);
    }

    public async Task SetTaskEnabledAsync(int id, bool enabled)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync();
        
        var task = await context.ScheduledTasks.FindAsync(id);
        if (task == null)
            throw new InvalidOperationException($"任务不存在: {id}");
        
        task.IsEnabled = enabled;
        task.UpdatedAt = DateTime.UtcNow;
        
        if (enabled && task.IntervalMinutes > 0)
        {
            task.NextRunTime = DateTime.UtcNow.AddMinutes(task.IntervalMinutes);
        }
        else
        {
            task.NextRunTime = null;
        }
        
        await context.SaveChangesAsync();
        
        _logger.LogInformation("{Action}定时任务: {TaskName}", enabled ? "启用" : "禁用", task.Name);
    }

    public async Task RunTaskNowAsync(int id)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync();
        
        var task = await context.ScheduledTasks.FindAsync(id);
        if (task == null)
            throw new InvalidOperationException($"任务不存在: {id}");
        
        // Set next run time to now (will be picked up by background service)
        task.NextRunTime = DateTime.UtcNow;
        await context.SaveChangesAsync();
        
        _logger.LogInformation("立即运行定时任务: {TaskName}", task.Name);
    }

    public async Task StopTaskAsync(int id)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync();
        
        var task = await context.ScheduledTasks.FindAsync(id);
        if (task == null)
            throw new InvalidOperationException($"任务不存在: {id}");
        
        if (task.Status != "Running")
        {
            _logger.LogWarning("任务不在运行状态，无法停止: {TaskName} (当前状态: {Status})", task.Name, task.Status);
            return;
        }
        
        // Mark task as stopped - the executor service will check this
        task.Status = "Stopped";
        task.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        
        _logger.LogInformation("标记定时任务为停止: {TaskName}", task.Name);
    }

    public async Task<List<ScheduledTask>> GetPendingTasksAsync()
    {
        using var context = await _dbContextFactory.CreateDbContextAsync();
        
        var now = DateTime.UtcNow;
        
        return await context.ScheduledTasks
            .Where(t => t.IsEnabled && 
                       t.NextRunTime != null && 
                       t.NextRunTime <= now &&
                       t.Status != "Running")
            .ToListAsync();
    }

    public async Task UpdateTaskStatusAsync(int id, string status, DateTime? nextRunTime, string? error = null)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync();
        
        var task = await context.ScheduledTasks.FindAsync(id);
        if (task == null)
            return;
        
        task.Status = status;
        task.NextRunTime = nextRunTime;
        task.LastError = error;
        task.UpdatedAt = DateTime.UtcNow;
        
        if (status == "Completed" || status == "Failed")
        {
            task.LastRunTime = DateTime.UtcNow;
        }
        
        await context.SaveChangesAsync();
    }
    
    public bool ValidateCronExpression(string cronExpression)
    {
        if (string.IsNullOrWhiteSpace(cronExpression))
            return false;
            
        try
        {
            CronExpression.Parse(cronExpression, CronFormat.IncludeSeconds);
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    public DateTime? CalculateNextRunTime(ScheduledTask task)
    {
        try
        {
            // If it's a one-time task that has already run, return null
            if (task.IsOneTime && task.LastRunTime != null)
                return null;
                
            // If cron expression is specified, use it (takes precedence)
            if (!string.IsNullOrWhiteSpace(task.CronExpression))
            {
                var cron = CronExpression.Parse(task.CronExpression, CronFormat.IncludeSeconds);
                return cron.GetNextOccurrence(DateTime.UtcNow, TimeZoneInfo.Local);
            }
            
            // Fall back to interval-based scheduling
            if (task.IntervalMinutes > 0)
            {
                return DateTime.UtcNow.AddMinutes(task.IntervalMinutes);
            }
            
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate next run time for task {TaskId}", task.Id);
            return null;
        }
    }
}

