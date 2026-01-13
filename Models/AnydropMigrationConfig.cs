namespace QingFeng.Models;

/// <summary>
/// Configuration for Anydrop file migration scheduled task.
/// Used when the Anydrop storage directory is changed and files need to be moved.
/// </summary>
public class AnydropMigrationConfig
{
    /// <summary>
    /// Source directory containing existing Anydrop files
    /// </summary>
    public string SourceDirectory { get; set; } = string.Empty;
    
    /// <summary>
    /// Destination directory where files should be moved
    /// </summary>
    public string DestinationDirectory { get; set; } = string.Empty;
}
