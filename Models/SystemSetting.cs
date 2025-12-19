namespace QingFeng.Models;

/// <summary>
/// Represents system-wide settings
/// </summary>
public class SystemSetting
{
    public int Id { get; set; }
    
    /// <summary>
    /// Setting key (unique identifier)
    /// </summary>
    public string Key { get; set; } = string.Empty;
    
    /// <summary>
    /// Setting value (stored as string, parsed by application as needed)
    /// </summary>
    public string Value { get; set; } = string.Empty;
    
    /// <summary>
    /// Setting category (e.g., "Appearance", "System", "Network")
    /// </summary>
    public string Category { get; set; } = string.Empty;
    
    /// <summary>
    /// Human-readable description
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Data type hint (e.g., "string", "int", "bool", "json")
    /// </summary>
    public string DataType { get; set; } = "string";
    
    /// <summary>
    /// Creation timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Last update timestamp
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
