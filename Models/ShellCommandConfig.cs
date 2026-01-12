namespace QingFeng.Models;

/// <summary>
/// Configuration for shell command scheduled tasks.
/// 
/// ⚠️ CRITICAL SECURITY WARNING ⚠️
/// - Commands are executed via /bin/bash -c WITHOUT input sanitization
/// - Quote escaping is minimal and NOT sufficient to prevent all injection attacks
/// - Only allow TRUSTED ADMINISTRATORS to create shell command tasks
/// - NEVER include user input directly in command strings
/// - Working directories are validated against FileManagerService.IsPathAllowed()
/// - See doc/SECURITY_SHELL_COMMANDS.md for complete security guidelines and best practices
/// </summary>
public class ShellCommandConfig
{
    /// <summary>
    /// Shell command to execute (passed to /bin/bash -c).
    /// ⚠️ WARNING: Commands are NOT sanitized. Only basic quote escaping is performed.
    /// See SECURITY_SHELL_COMMANDS.md for safe command construction guidelines.
    /// </summary>
    public string Command { get; set; } = string.Empty;
    
    /// <summary>
    /// Working directory for command execution (optional).
    /// Validated against FileManagerService.IsPathAllowed().
    /// Directory must exist or execution will fail.
    /// </summary>
    public string WorkingDirectory { get; set; } = string.Empty;
    
    /// <summary>
    /// Timeout in seconds (default: 3600 seconds / 1 hour, max: 86400 seconds / 24 hours).
    /// </summary>
    public int TimeoutSeconds { get; set; } = 3600;
    
    /// <summary>
    /// Whether to capture stdout and stderr for logging and history.
    /// </summary>
    public bool CaptureOutput { get; set; } = true;
}
