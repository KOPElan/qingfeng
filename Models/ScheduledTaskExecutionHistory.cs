namespace QingFeng.Models;

public class ScheduledTaskExecutionHistory
{
    public int Id { get; set; }
    public int ScheduledTaskId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string Status { get; set; } = string.Empty; // "Success", "Failed", "Cancelled"
    public string? Result { get; set; } // Execution result or output
    public string? ErrorMessage { get; set; }
    public int? DurationMs { get; set; } // Duration in milliseconds
    
    // Navigation property
    public ScheduledTask? ScheduledTask { get; set; }
}
