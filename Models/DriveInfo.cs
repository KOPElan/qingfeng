using QingFeng.Utilities;

namespace QingFeng.Models;

public class DriveItemInfo
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public long TotalSize { get; set; }
    public long AvailableSize { get; set; }
    public string DriveType { get; set; } = string.Empty;
    
    public string TotalSizeDisplay => FileUtilities.FormatSize(TotalSize);
    public string AvailableSizeDisplay => FileUtilities.FormatSize(AvailableSize);
    public string UsedSizeDisplay => FileUtilities.FormatSize(TotalSize - AvailableSize);
}

public class ShortcutItemInfo
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // documents, downloads, gallery, media, backup
}
