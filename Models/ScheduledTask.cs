namespace QingFeng.Models;

public class ScheduledTask
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TaskType { get; set; } = string.Empty; // "FileIndexing", etc.
    public string Configuration { get; set; } = string.Empty; // JSON configuration
    public bool IsEnabled { get; set; }
    public int IntervalMinutes { get; set; } // How often to run (in minutes) - legacy, kept for compatibility
    public string? CronExpression { get; set; } // Cron expression for scheduling (takes precedence over IntervalMinutes)
    public bool IsOneTime { get; set; } // If true, task runs once and is automatically disabled
    public DateTime? LastRunTime { get; set; }
    public DateTime? NextRunTime { get; set; }
    public string Status { get; set; } = "Idle"; // "Idle", "Running", "Failed", "Completed", "Stopped"
    public string? LastError { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
