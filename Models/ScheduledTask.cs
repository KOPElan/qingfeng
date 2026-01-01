namespace QingFeng.Models;

public class ScheduledTask
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TaskType { get; set; } = string.Empty; // "FileIndexing", etc.
    public string Configuration { get; set; } = string.Empty; // JSON configuration
    public bool IsEnabled { get; set; }
    public int IntervalMinutes { get; set; } // How often to run (in minutes)
    public DateTime? LastRunTime { get; set; }
    public DateTime? NextRunTime { get; set; }
    public string Status { get; set; } = "Idle"; // "Idle", "Running", "Failed", "Completed"
    public string? LastError { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
