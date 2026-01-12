namespace QingFeng.Models;

/// <summary>
/// Configuration for shell command scheduled tasks.
/// 
/// SECURITY WARNING: Commands are executed via /bin/bash -c without sanitization.
/// Only allow trusted administrators to create shell command tasks.
/// Never include user input directly in command strings.
/// Working directories are validated against FileManagerService.IsPathAllowed().
/// </summary>
public class ShellCommandConfig
{
    /// <summary>
    /// Shell command to execute (passed to /bin/bash -c).
    /// WARNING: No input sanitization is performed.
    /// </summary>
    public string Command { get; set; } = string.Empty;
    
    /// <summary>
    /// Working directory for command execution (optional).
    /// Validated against FileManagerService.IsPathAllowed().
    /// </summary>
    public string WorkingDirectory { get; set; } = string.Empty;
    
    /// <summary>
    /// Timeout in seconds (default: 3600 seconds / 1 hour).
    /// </summary>
    public int TimeoutSeconds { get; set; } = 3600;
    
    /// <summary>
    /// Whether to capture stdout and stderr.
    /// </summary>
    public bool CaptureOutput { get; set; } = true;
}
