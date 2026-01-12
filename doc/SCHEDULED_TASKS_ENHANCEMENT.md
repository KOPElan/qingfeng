# Scheduled Tasks Enhancement - Shell Command Support

## Overview

The scheduled tasks system has been enhanced to support arbitrary shell command execution as background tasks, implementing a true "launch and forget" pattern for both periodic and one-time tasks.

## Features

### Task Types

The system now supports two task types:

1. **FileIndexing** - File system indexing tasks (existing)
2. **ShellCommand** - Execute arbitrary shell commands (new)

### Scheduling Options

Tasks can be scheduled in three ways:

1. **Interval-based**: Run at fixed intervals (minutes)
2. **Cron-based**: Use cron expressions for complex schedules
3. **One-time**: Execute once and automatically disable

### Manual Execution

All tasks can be manually triggered to run immediately, regardless of their schedule, making them suitable for:
- Long-running background operations
- Manually triggered maintenance tasks
- On-demand processing jobs

## Shell Command Configuration

Shell command tasks support the following configuration options:

```json
{
  "Command": "your-shell-command",
  "WorkingDirectory": "/path/to/working/dir",  // Optional, defaults to application directory
  "TimeoutSeconds": 3600,                       // Default: 1 hour, Max: 24 hours
  "CaptureOutput": true                         // Capture stdout/stderr
}
```

### Examples

#### Example 1: One-time cleanup task

```json
{
  "name": "清理临时文件",
  "description": "清理系统临时目录中的旧文件",
  "taskType": "ShellCommand",
  "configuration": "{\"Command\":\"find /tmp -type f -mtime +7 -delete\",\"WorkingDirectory\":\"\",\"TimeoutSeconds\":300,\"CaptureOutput\":true}",
  "isEnabled": true,
  "intervalMinutes": 0,
  "cronExpression": null,
  "isOneTime": true
}
```

#### Example 2: Recurring backup task

```json
{
  "name": "数据库备份",
  "description": "每天凌晨2点备份数据库",
  "taskType": "ShellCommand",
  "configuration": "{\"Command\":\"mysqldump -u user -p password mydb > /backup/mydb_$(date +%Y%m%d).sql\",\"WorkingDirectory\":\"/backup\",\"TimeoutSeconds\":1800,\"CaptureOutput\":true}",
  "isEnabled": true,
  "intervalMinutes": 0,
  "cronExpression": "0 0 2 * * *",
  "isOneTime": false
}
```

#### Example 3: System monitoring

```json
{
  "name": "系统监控",
  "description": "每5分钟检查系统状态",
  "taskType": "ShellCommand",
  "configuration": "{\"Command\":\"df -h; free -m; uptime\",\"WorkingDirectory\":\"\",\"TimeoutSeconds\":30,\"CaptureOutput\":true}",
  "isEnabled": true,
  "intervalMinutes": 5,
  "cronExpression": null,
  "isOneTime": false
}
```

## API Usage

### Create a Shell Command Task

```bash
curl -X POST http://localhost:5000/api/tasks \
  -H "Content-Type: application/json" \
  -d '{
    "name": "My Task",
    "description": "Task description",
    "taskType": "ShellCommand",
    "configuration": "{\"Command\":\"echo Hello\",\"TimeoutSeconds\":30,\"CaptureOutput\":true}",
    "isEnabled": true,
    "intervalMinutes": 60,
    "isOneTime": false
  }'
```

### Run a Task Immediately

```bash
curl -X POST http://localhost:5000/api/tasks/{id}/run
```

### Enable/Disable a Task

```bash
curl -X POST http://localhost:5000/api/tasks/{id}/enable \
  -H "Content-Type: application/json" \
  -d '{"enabled": true}'
```

### Stop a Running Task

```bash
curl -X POST http://localhost:5000/api/tasks/{id}/stop
```

## Execution Details

### Process Management

- Commands are executed using `/bin/bash -c`
- Each task runs in a separate process
- Timeout handling prevents runaway processes
- Cancellation tokens allow graceful task termination

### Output Capture

When `CaptureOutput` is enabled:
- Standard output (stdout) is captured and logged
- Standard error (stderr) is captured and logged separately
- All output is available in execution history
- Real-time logging during execution

### Error Handling

- Non-zero exit codes are treated as failures
- Timeout exceptions are properly caught and reported
- Execution history records success/failure status
- Error messages and stack traces are preserved

### Security Considerations

⚠️ **Important Security Notes:**

1. **Permissions**: Commands run with the same permissions as the application
2. **Input Validation**: No input sanitization is performed - use with caution
3. **Access Control**: Ensure only authorized users can create tasks
4. **Sensitive Data**: Avoid including passwords or secrets in command strings
5. **Path Validation**: Consider restricting accessible paths

## UI Features

The scheduled tasks UI now includes:

1. **Task Type Selection**: Choose between FileIndexing and ShellCommand
2. **Shell Command Editor**: Multi-line text area for command input
3. **Configuration Options**: 
   - Working directory (optional)
   - Timeout configuration
   - Output capture toggle
4. **Task Status Display**: Real-time status updates
5. **Execution History**: View past executions with output/errors
6. **Manual Controls**: Run, Stop, Enable/Disable buttons

## Implementation Details

### New Files

- `Models/ShellCommandConfig.cs` - Configuration model for shell commands

### Modified Files

- `Services/ScheduledTaskExecutorService.cs` - Added shell command execution logic
- `Components/Pages/ScheduledTasks.razor` - UI updates for shell command tasks

### Code Structure

```csharp
private async Task ExecuteShellCommandTaskAsync(
    int taskId, 
    string configuration, 
    CancellationToken cancellationToken, 
    IScheduledTaskExecutionHistoryService historyService)
{
    // Parse configuration
    var config = JsonSerializer.Deserialize<ShellCommandConfig>(configuration);
    
    // Create process
    var processStartInfo = new ProcessStartInfo
    {
        FileName = "/bin/bash",
        Arguments = $"-c \"{command}\"",
        // ... configuration
    };
    
    // Execute with timeout and cancellation support
    // Capture output
    // Handle errors
}
```

## Best Practices

1. **Test Commands First**: Test shell commands manually before creating tasks
2. **Use Absolute Paths**: Always use absolute paths in commands
3. **Set Appropriate Timeouts**: Consider command execution time when setting timeouts
4. **Monitor Execution History**: Regularly check execution history for failures
5. **Enable Output Capture**: Use output capture for debugging
6. **Handle Long-Running Tasks**: For very long tasks, consider creating dedicated services instead
7. **Use Cron for Complex Schedules**: Cron expressions provide more flexibility than intervals

## Troubleshooting

### Task Not Executing

1. Check if task is enabled (`isEnabled: true`)
2. Verify next run time is in the past
3. Check task status is not "Running"
4. Review server logs for errors

### Command Fails

1. Test command in terminal first
2. Check command permissions
3. Verify working directory exists
4. Review error message in execution history
5. Check timeout is sufficient

### Output Not Captured

1. Ensure `CaptureOutput: true` in configuration
2. Check execution history for output
3. Verify command writes to stdout/stderr
4. Review server logs for captured output

## Future Enhancements

Potential future improvements:

1. **Environment Variables**: Support for custom environment variables
2. **Script Upload**: Allow uploading and executing script files
3. **Notification**: Email/webhook notifications on task completion/failure
4. **Resource Limits**: CPU and memory limits for tasks
5. **Task Dependencies**: Chain tasks based on success/failure
6. **Retry Logic**: Automatic retry on failure with backoff
7. **Task Templates**: Pre-defined task templates for common operations
