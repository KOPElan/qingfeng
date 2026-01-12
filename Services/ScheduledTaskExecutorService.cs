using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using QingFeng.Models;

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
            CancellationTokenSource? cts = null;
            lock (_runningTasksLock)
            {
                if (_runningTasks.TryGetValue(task.Id, out var existingCts))
                {
                    cts = existingCts;
                    _runningTasks.Remove(task.Id);
                }
            }

            if (cts != null)
            {
                cts.Cancel();
                cts.Dispose();
                _logger.LogInformation("取消正在运行的任务: TaskId={TaskId}", task.Id);
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
            
            // Check if task is already running to avoid resource leak
            lock (_runningTasksLock)
            {
                if (_runningTasks.ContainsKey(task.Id))
                {
                    _logger.LogWarning("任务已在运行中，跳过: TaskId={TaskId}", task.Id);
                    continue;
                }
            }
            
            // Execute task in background (don't wait)
            // Link the task CancellationTokenSource with the service's cancellation token
            var taskCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
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
            }, CancellationToken.None); // Don't pass cancellation token here to avoid immediate cancellation
        }
    }

    private async Task ExecuteTaskAsync(int taskId, string taskType, string configuration, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var taskService = scope.ServiceProvider.GetRequiredService<IScheduledTaskService>();
        var historyService = scope.ServiceProvider.GetRequiredService<IScheduledTaskExecutionHistoryService>();
        
        var startTime = DateTime.UtcNow;
        var history = new ScheduledTaskExecutionHistory
        {
            ScheduledTaskId = taskId,
            StartTime = startTime,
            Status = "Running"
        };
        
        try
        {
            // Create history record
            history = await historyService.CreateHistoryAsync(history);
            
            _logger.LogInformation("开始执行定时任务: ID={TaskId}, Type={TaskType}", taskId, taskType);
            
            // Update status to Running
            await taskService.UpdateTaskStatusAsync(taskId, "Running", null);
            
            // Execute based on task type
            switch (taskType)
            {
                case "FileIndexing":
                    await ExecuteFileIndexingTaskAsync(configuration, cancellationToken);
                    break;
                case "ShellCommand":
                    await ExecuteShellCommandTaskAsync(taskId, configuration, cancellationToken);
                    break;
                case "AnydropMigration":
                    await ExecuteAnydropMigrationTaskAsync(configuration, cancellationToken);
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
                
                // Update history
                history.Status = "Cancelled";
                history.EndTime = DateTime.UtcNow;
                history.DurationMs = (long)(history.EndTime.Value - history.StartTime).TotalMilliseconds;
                await historyService.UpdateHistoryAsync(history);
                return;
            }
            
            // Calculate next run time
            DateTime? nextRunTime = null;
            if (task != null && task.IsEnabled)
            {
                // If it's a one-time task, disable it after execution
                if (task.IsOneTime)
                {
                    await taskService.SetTaskEnabledAsync(taskId, false);
                    _logger.LogInformation("一次性任务已执行完成并禁用: ID={TaskId}", taskId);
                }
                else
                {
                    // Calculate next run time for recurring tasks
                    nextRunTime = taskService.CalculateNextRunTime(task);
                }
            }
            
            await taskService.UpdateTaskStatusAsync(taskId, "Completed", nextRunTime);
            _logger.LogInformation("定时任务执行成功: ID={TaskId}, 下次执行时间: {NextRunTime}", taskId, nextRunTime);
            
            // Update history
            history.Status = "Success";
            history.EndTime = DateTime.UtcNow;
            history.DurationMs = (long)(history.EndTime.Value - history.StartTime).TotalMilliseconds;
            history.Result = "任务执行成功";
            await historyService.UpdateHistoryAsync(history);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("定时任务被取消: ID={TaskId}", taskId);
            await taskService.UpdateTaskStatusAsync(taskId, "Idle", null, "任务被取消");
            
            // Update history
            history.Status = "Cancelled";
            history.EndTime = DateTime.UtcNow;
            history.DurationMs = (long)(history.EndTime.Value - history.StartTime).TotalMilliseconds;
            history.ErrorMessage = "任务被取消";
            await historyService.UpdateHistoryAsync(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "执行定时任务失败: ID={TaskId}", taskId);
            
            // Get the task again to calculate next run time even on failure
            var task = await taskService.GetTaskAsync(taskId);
            DateTime? nextRunTime = null;
            if (task != null && task.IsEnabled)
            {
                nextRunTime = taskService.CalculateNextRunTime(task);
            }
            
            await taskService.UpdateTaskStatusAsync(taskId, "Failed", nextRunTime, ex.Message);
            
            // Update history
            history.Status = "Failed";
            history.EndTime = DateTime.UtcNow;
            history.DurationMs = (long)(history.EndTime.Value - history.StartTime).TotalMilliseconds;
            history.ErrorMessage = ex.Message;
            history.Result = ex.StackTrace;
            await historyService.UpdateHistoryAsync(history);
        }
    }

    private async Task ExecuteFileIndexingTaskAsync(string configuration, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var fileIndexService = scope.ServiceProvider.GetRequiredService<IFileIndexService>();
        var fileManagerService = scope.ServiceProvider.GetRequiredService<IFileManagerService>();
        
        // Parse configuration to get root path
        var config = JsonSerializer.Deserialize<FileIndexingConfig>(configuration);
        if (config == null || string.IsNullOrEmpty(config.RootPath))
        {
            throw new InvalidOperationException("文件索引任务配置无效");
        }
        
        // Validate path is allowed for security
        if (!fileManagerService.IsPathAllowed(config.RootPath))
        {
            throw new UnauthorizedAccessException($"不允许访问路径: {config.RootPath}");
        }
        
        _logger.LogInformation("开始重建文件索引: {RootPath}", config.RootPath);
        await fileIndexService.RebuildIndexAsync(config.RootPath);
        _logger.LogInformation("文件索引重建完成: {RootPath}", config.RootPath);
    }

    private async Task ExecuteShellCommandTaskAsync(int taskId, string configuration, CancellationToken cancellationToken)
    {
        // Parse configuration
        var config = JsonSerializer.Deserialize<ShellCommandConfig>(configuration);
        if (config == null || string.IsNullOrEmpty(config.Command))
        {
            throw new InvalidOperationException("Shell命令任务配置无效");
        }
        
        // Validate working directory path if specified
        if (!string.IsNullOrEmpty(config.WorkingDirectory))
        {
            using var scope = _serviceProvider.CreateScope();
            var fileManagerService = scope.ServiceProvider.GetRequiredService<IFileManagerService>();
            
            if (!fileManagerService.IsPathAllowed(config.WorkingDirectory))
            {
                throw new UnauthorizedAccessException($"不允许访问工作目录: {config.WorkingDirectory}");
            }
            
            if (!Directory.Exists(config.WorkingDirectory))
            {
                throw new InvalidOperationException($"工作目录不存在: {config.WorkingDirectory}");
            }
        }

        _logger.LogInformation("开始执行Shell命令: {Command}", config.Command);

        var outputBuilder = new System.Text.StringBuilder();
        var errorBuilder = new System.Text.StringBuilder();

        try
        {
            // ⚠️ SECURITY NOTE: Commands are executed via /bin/bash -c with only basic quote escaping.
            // This approach is chosen for maximum flexibility but requires strict access control.
            // REQUIREMENTS for callers:
            // 1. Only allow trusted administrators to create shell command tasks
            // 2. Never include unsanitized user input in command strings
            // 3. Implement command allowlist or approval workflow in production
            // 4. See doc/SECURITY_SHELL_COMMANDS.md for complete security guidelines
            //
            // Alternative considered: ProcessStartInfo with argument arrays would be safer but
            // doesn't support complex shell commands (pipes, redirects, etc.) which is the
            // primary use case for this feature.
            var processStartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"{config.Command.Replace("\"", "\\\"")}\"",
                RedirectStandardOutput = config.CaptureOutput,
                RedirectStandardError = config.CaptureOutput,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = string.IsNullOrEmpty(config.WorkingDirectory) ? Environment.CurrentDirectory : config.WorkingDirectory
            };

            using var process = new System.Diagnostics.Process { StartInfo = processStartInfo };
            
            if (config.CaptureOutput)
            {
                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        outputBuilder.AppendLine(e.Data);
                        _logger.LogInformation("[TaskId={TaskId}] {Output}", taskId, e.Data);
                    }
                };
                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        errorBuilder.AppendLine(e.Data);
                        _logger.LogWarning("[TaskId={TaskId}] {Error}", taskId, e.Data);
                    }
                };
            }

            process.Start();

            if (config.CaptureOutput)
            {
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
            }

            // Wait for the process to exit with timeout
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(config.TimeoutSeconds), cancellationToken);
            var processTask = process.WaitForExitAsync(cancellationToken);
            
            var completedTask = await Task.WhenAny(processTask, timeoutTask);
            
            if (completedTask == timeoutTask)
            {
                // Timeout occurred
                try
                {
                    process.Kill(true);
                }
                catch { }
                throw new TimeoutException($"命令执行超时（超过 {config.TimeoutSeconds} 秒）");
            }

            // Process completed
            var exitCode = process.ExitCode;
            var output = outputBuilder.ToString();
            var errorOutput = errorBuilder.ToString();

            if (exitCode != 0)
            {
                var errorMessage = $"命令执行失败，退出码: {exitCode}";
                if (!string.IsNullOrEmpty(errorOutput))
                {
                    errorMessage += $"\n错误输出:\n{errorOutput}";
                }
                throw new InvalidOperationException(errorMessage);
            }

            _logger.LogInformation("Shell命令执行成功: {Command}, 退出码: {ExitCode}", config.Command, exitCode);

            // Output capture note: Command output is logged in real-time above (via OutputDataReceived).
            // The execution history is updated by the calling method (ExecuteTaskAsync) which handles
            // the overall task lifecycle including success/failure status and duration.
            // Individual command outputs are visible in server logs with [TaskId=X] prefix.
            if (!string.IsNullOrEmpty(output))
            {
                _logger.LogInformation("命令输出:\n{Output}", output);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "执行Shell命令失败: {Command}", config.Command);
            throw;
        }
    }

    private async Task ExecuteAnydropMigrationTaskAsync(string configuration, CancellationToken cancellationToken)
    {
        // Parse configuration
        var config = JsonSerializer.Deserialize<AnydropMigrationConfig>(configuration);
        if (config == null || string.IsNullOrEmpty(config.SourceDirectory) || string.IsNullOrEmpty(config.DestinationDirectory))
        {
            throw new InvalidOperationException("Anydrop迁移任务配置无效");
        }

        _logger.LogInformation("开始Anydrop文件迁移: {Source} -> {Destination}", config.SourceDirectory, config.DestinationDirectory);

        if (!Directory.Exists(config.SourceDirectory))
        {
            _logger.LogWarning("源目录不存在: {Source}，无需迁移", config.SourceDirectory);
            return;
        }

        // Create destination directory if it doesn't exist
        if (!Directory.Exists(config.DestinationDirectory))
        {
            Directory.CreateDirectory(config.DestinationDirectory);
            _logger.LogInformation("创建目标目录: {Destination}", config.DestinationDirectory);
        }

        // Get all files in the source directory
        var files = Directory.GetFiles(config.SourceDirectory, "*", SearchOption.AllDirectories);
        var totalFiles = files.Length;
        var movedFiles = 0;
        var failedFiles = 0;

        _logger.LogInformation("准备迁移 {TotalFiles} 个文件", totalFiles);

        foreach (var sourceFile in files)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Anydrop迁移任务被取消");
                throw new OperationCanceledException();
            }

            try
            {
                // Calculate relative path
                var relativePath = Path.GetRelativePath(config.SourceDirectory, sourceFile);
                var destinationFile = Path.Combine(config.DestinationDirectory, relativePath);

                // Create subdirectories if needed
                var destinationDir = Path.GetDirectoryName(destinationFile);
                if (!string.IsNullOrEmpty(destinationDir) && !Directory.Exists(destinationDir))
                {
                    Directory.CreateDirectory(destinationDir);
                }

                // Move the file using copy + delete for better error handling
                if (File.Exists(destinationFile))
                {
                    // File exists, try to create a unique name
                    var fileName = Path.GetFileNameWithoutExtension(destinationFile);
                    var extension = Path.GetExtension(destinationFile);
                    var directory = Path.GetDirectoryName(destinationFile) ?? config.DestinationDirectory;
                    var counter = 1;
                    
                    while (File.Exists(destinationFile) && counter < 1000)
                    {
                        destinationFile = Path.Combine(directory, $"{fileName}_copy_{counter}{extension}");
                        counter++;
                    }
                    
                    if (File.Exists(destinationFile))
                    {
                        _logger.LogWarning("目标文件已存在且无法创建唯一名称，跳过: {File}", relativePath);
                        failedFiles++;
                        continue;
                    }
                    
                    _logger.LogInformation("目标文件已存在，使用新名称: {NewName}", Path.GetFileName(destinationFile));
                }
                
                try
                {
                    // Use copy + delete for better error handling and data integrity
                    File.Copy(sourceFile, destinationFile, false);
                    
                    // Verify the copy was successful before deleting source
                    if (File.Exists(destinationFile))
                    {
                        File.Delete(sourceFile);
                        movedFiles++;
                        
                        if (movedFiles % 100 == 0)
                        {
                            _logger.LogInformation("已迁移 {MovedFiles}/{TotalFiles} 个文件", movedFiles, totalFiles);
                        }
                    }
                    else
                    {
                        _logger.LogError("文件复制后验证失败: {File}", sourceFile);
                        failedFiles++;
                    }
                }
                catch (IOException ioEx)
                {
                    _logger.LogError(ioEx, "文件迁移IO错误（可能文件正在使用）: {File}", sourceFile);
                    failedFiles++;
                }
                catch (UnauthorizedAccessException uaEx)
                {
                    _logger.LogError(uaEx, "文件迁移权限错误: {File}", sourceFile);
                    failedFiles++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "迁移文件失败: {File}", sourceFile);
                failedFiles++;
            }
        }

        // Try to remove empty directories in source
        try
        {
            var directories = Directory.GetDirectories(config.SourceDirectory, "*", SearchOption.AllDirectories)
                .OrderByDescending(d => d.Length); // Process deepest directories first

            foreach (var dir in directories)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    if (!Directory.EnumerateFileSystemEntries(dir).Any())
                    {
                        Directory.Delete(dir);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "无法删除空目录: {Directory}", dir);
                }
            }

            // Try to remove the root source directory if empty
            if (!Directory.EnumerateFileSystemEntries(config.SourceDirectory).Any())
            {
                Directory.Delete(config.SourceDirectory);
                _logger.LogInformation("源目录已清空并删除: {Source}", config.SourceDirectory);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "清理源目录时发生错误");
        }

        _logger.LogInformation("Anydrop文件迁移完成: 成功 {MovedFiles} 个, 失败 {FailedFiles} 个, 总计 {TotalFiles} 个", 
            movedFiles, failedFiles, totalFiles);
    }
}
