namespace QingFeng.Models;

/// <summary>
/// Represents a shortcut item stored in the database
/// </summary>
public class ShortcutItem
{
    public int Id { get; set; }
    public string ShortcutId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool IsPinned { get; set; }
    public bool IsDocker { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Converts the ShortcutItem to a ShortcutLink for UI usage
    /// </summary>
    public ShortcutLink ToShortcutLink()
    {
        return new ShortcutLink
        {
            Id = ShortcutId,
            Title = Title,
            Url = Url,
            Icon = Icon,
            Description = Description,
            Category = Category,
            IsPinned = IsPinned,
            IsDocker = IsDocker
        };
    }

    /// <summary>
    /// Creates a ShortcutItem from a ShortcutLink
    /// </summary>
    public static ShortcutItem FromShortcutLink(ShortcutLink link, int sortOrder = 0)
    {
        return new ShortcutItem
        {
            ShortcutId = link.Id,
            Title = link.Title,
            Url = link.Url,
            Icon = link.Icon,
            Description = link.Description,
            Category = link.Category,
            IsPinned = link.IsPinned,
            IsDocker = link.IsDocker,
            SortOrder = sortOrder
        };
    }
}
