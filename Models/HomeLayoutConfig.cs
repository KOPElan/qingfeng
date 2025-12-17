namespace QingFeng.Models;

/// <summary>
/// Represents the home page layout configuration
/// </summary>
public class HomeLayoutConfig
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
