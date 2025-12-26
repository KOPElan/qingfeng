namespace QingFeng.Models;

/// <summary>
/// Represents the status of a required feature/package
/// </summary>
public enum FeatureStatus
{
    Available,
    Missing,
    NotApplicable
}

/// <summary>
/// Represents a feature or package requirement
/// </summary>
public class FeatureRequirement
{
    /// <summary>
    /// Name of the feature or package
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Description of what this feature/package does
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Current status of the feature
    /// </summary>
    public FeatureStatus Status { get; set; }
    
    /// <summary>
    /// Whether this feature is required for basic functionality
    /// </summary>
    public bool IsRequired { get; set; }
    
    /// <summary>
    /// Command or package name to check
    /// </summary>
    public string CheckCommand { get; set; } = string.Empty;
    
    /// <summary>
    /// Installation command/instructions
    /// </summary>
    public string InstallCommand { get; set; } = string.Empty;
    
    /// <summary>
    /// Additional notes or instructions
    /// </summary>
    public string Notes { get; set; } = string.Empty;
}

/// <summary>
/// Results of disk management feature detection
/// </summary>
public class DiskManagementFeatureDetection
{
    public List<FeatureRequirement> Requirements { get; set; } = new();
    public bool AllRequiredFeaturesAvailable { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string DetectedOS { get; set; } = string.Empty;
}

/// <summary>
/// Results of share management feature detection
/// </summary>
public class ShareManagementFeatureDetection
{
    public List<FeatureRequirement> Requirements { get; set; } = new();
    public bool AllRequiredFeaturesAvailable { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string DetectedOS { get; set; } = string.Empty;
}
