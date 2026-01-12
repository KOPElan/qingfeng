namespace QingFeng.Models;

public class ShellCommandConfig
{
    public string Command { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 3600; // Default 1 hour timeout
    public bool CaptureOutput { get; set; } = true;
}
