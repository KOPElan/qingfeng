namespace QingFeng.Models;

/// <summary>
/// Represents an application in the application center
/// Can be self-hosted applications or other custom links
/// </summary>
public class Application
{
    public int Id { get; set; }
    
    /// <summary>
    /// Unique identifier for the application
    /// </summary>
    public string AppId { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Application name/title
    /// </summary>
    public string Title { get; set; } = string.Empty;
    
    /// <summary>
    /// Application URL
    /// </summary>
    public string Url { get; set; } = string.Empty;
    
    /// <summary>
    /// Icon name or URL (e.g., "bi-app" or a URL to an icon)
    /// </summary>
    public string Icon { get; set; } = "bi-app";
    
    /// <summary>
    /// Icon background color (e.g., "#3b82f6")
    /// </summary>
    public string IconColor { get; set; } = "#3b82f6";
    
    /// <summary>
    /// Application description
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Category/Tag for the application (e.g., "MEDIA", "NAS", "DOCKER")
    /// </summary>
    public string Category { get; set; } = string.Empty;
    
    /// <summary>
    /// Status indicator (online/offline)
    /// </summary>
    public bool IsOnline { get; set; } = true;
    
    /// <summary>
    /// Sort order for display
    /// </summary>
    public int SortOrder { get; set; }
    
    /// <summary>
    /// Whether the application is pinned to dock
    /// </summary>
    public bool IsPinnedToDock { get; set; }
    
    /// <summary>
    /// Creation timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Last update timestamp
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
