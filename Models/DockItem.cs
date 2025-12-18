namespace QingFeng.Models;

/// <summary>
/// Represents an item in the dock navigation bar
/// </summary>
public class DockItem
{
    public int Id { get; set; }
    
    /// <summary>
    /// Unique identifier for the dock item
    /// </summary>
    public string ItemId { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Item title
    /// </summary>
    public string Title { get; set; } = string.Empty;
    
    /// <summary>
    /// Icon name (e.g., "bi-house-door", "dashboard")
    /// </summary>
    public string Icon { get; set; } = string.Empty;
    
    /// <summary>
    /// Navigation URL (e.g., "/", "/settings")
    /// </summary>
    public string Url { get; set; } = string.Empty;
    
    /// <summary>
    /// Icon background gradient CSS (e.g., "var(--dock-gradient-dashboard)")
    /// </summary>
    public string IconBackground { get; set; } = string.Empty;
    
    /// <summary>
    /// Icon color
    /// </summary>
    public string IconColor { get; set; } = "white";
    
    /// <summary>
    /// Sort order for dock display
    /// </summary>
    public int SortOrder { get; set; }
    
    /// <summary>
    /// Whether this is a system item (Home, Settings) that cannot be removed
    /// </summary>
    public bool IsSystemItem { get; set; }
    
    /// <summary>
    /// Associated application ID (if this dock item is linked to an application)
    /// </summary>
    public string? AssociatedAppId { get; set; }
    
    /// <summary>
    /// Creation timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Last update timestamp
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
