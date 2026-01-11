using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace QingFeng.Services;

public class ScheduledTaskExecutorService : BackgroundService
{
    private readonly ILogger<ScheduledTaskExecutorService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1);
    private readonly Dictionary<int, CancellationTokenSource> _runningTasks = new();
    private readonly object _runningTasksLock = new();

    public ScheduledTaskExecutorService(
        ILogger<ScheduledTaskExecutorService> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("定时任务执行服务已启动");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndExecutePendingTasksAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "检查和执行定时任务时发生错误");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("定时任务执行服务已停止");
    }

    private async Task CheckAndExecutePendingTasksAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var taskService = scope.ServiceProvider.GetRequiredService<IScheduledTaskService>();
        
        // Check for tasks that were marked as "Stopped" and cancel them
        var allTasks = await taskService.GetAllTasksAsync();
        foreach (var task in allTasks.Where(t => t.Status == "Stopped"))
        {
            lock (_runningTasksLock)
            {
                if (_runningTasks.TryGetValue(task.Id, out var cts))
                {
                    cts.Cancel();
                    _logger.LogInformation("取消正在运行的任务: TaskId={TaskId}", task.Id);
                }
            }
        }
        
        var pendingTasks = await taskService.GetPendingTasksAsync();
        
        if (pendingTasks.Count > 0)
        {
            _logger.LogInformation("发现 {Count} 个待执行的定时任务", pendingTasks.Count);
        }
        
        foreach (var task in pendingTasks)
        {
            if (cancellationToken.IsCancellationRequested)
                break;
            
            // Execute task in background (don't wait)
            var taskCts = new CancellationTokenSource();
            lock (_runningTasksLock)
            {
                _runningTasks[task.Id] = taskCts;
            }
            
            _ = Task.Run(async () =>
            {
                try
                {
                    await ExecuteTaskAsync(task.Id, task.TaskType, task.Configuration, taskCts.Token);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "执行定时任务时发生未处理的异常: TaskId={TaskId}", task.Id);
                }
                finally
                {
                    lock (_runningTasksLock)
                    {
                        _runningTasks.Remove(task.Id);
                    }
                    taskCts.Dispose();
                }
            }, cancellationToken);
        }
    }

    private async Task ExecuteTaskAsync(int taskId, string taskType, string configuration, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var taskService = scope.ServiceProvider.GetRequiredService<IScheduledTaskService>();
        
        try
        {
            _logger.LogInformation("开始执行定时任务: ID={TaskId}, Type={TaskType}", taskId, taskType);
            
            // Update status to Running
            await taskService.UpdateTaskStatusAsync(taskId, "Running", null);
            
            // Execute based on task type
            switch (taskType)
            {
                case "FileIndexing":
                    await ExecuteFileIndexingTaskAsync(configuration, cancellationToken);
                    break;
                default:
                    _logger.LogWarning("未知的任务类型: {TaskType}", taskType);
                    break;
            }
            
            // Check if task was stopped during execution
            var task = await taskService.GetTaskAsync(taskId);
            if (task?.Status == "Stopped")
            {
                _logger.LogInformation("定时任务已被停止: ID={TaskId}", taskId);
                await taskService.UpdateTaskStatusAsync(taskId, "Idle", null);
                return;
            }
            
            // Get the task again to calculate next run time
            if (task != null && task.IsEnabled && task.IntervalMinutes > 0)
            {
                var nextRunTime = DateTime.UtcNow.AddMinutes(task.IntervalMinutes);
                await taskService.UpdateTaskStatusAsync(taskId, "Completed", nextRunTime);
                _logger.LogInformation("定时任务执行成功: ID={TaskId}, 下次执行时间: {NextRunTime}", taskId, nextRunTime);
            }
            else
            {
                await taskService.UpdateTaskStatusAsync(taskId, "Completed", null);
                _logger.LogInformation("定时任务执行成功: ID={TaskId}", taskId);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("定时任务被取消: ID={TaskId}", taskId);
            await taskService.UpdateTaskStatusAsync(taskId, "Idle", null, "任务被取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "执行定时任务失败: ID={TaskId}", taskId);
            
            // Get the task again to calculate next run time even on failure
            var task = await taskService.GetTaskAsync(taskId);
            DateTime? nextRunTime = null;
            if (task != null && task.IsEnabled && task.IntervalMinutes > 0)
            {
                nextRunTime = DateTime.UtcNow.AddMinutes(task.IntervalMinutes);
            }
            
            await taskService.UpdateTaskStatusAsync(taskId, "Failed", nextRunTime, ex.Message);
        }
        finally
        {
            lock (_runningTasksLock)
            {
                _runningTasks.Remove(taskId);
            }
        }
    }

    private async Task ExecuteFileIndexingTaskAsync(string configuration, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var fileIndexService = scope.ServiceProvider.GetRequiredService<IFileIndexService>();
        
        // Parse configuration to get root path
        var config = JsonSerializer.Deserialize<FileIndexingConfig>(configuration);
        if (config == null || string.IsNullOrEmpty(config.RootPath))
        {
            throw new InvalidOperationException("文件索引任务配置无效");
        }
        
        _logger.LogInformation("开始重建文件索引: {RootPath}", config.RootPath);
        await fileIndexService.RebuildIndexAsync(config.RootPath);
        _logger.LogInformation("文件索引重建完成: {RootPath}", config.RootPath);
    }

    private class FileIndexingConfig
    {
        public string RootPath { get; set; } = string.Empty;
    }
}
