namespace QingFeng.Models;

public class DriveItemInfo
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public long TotalSize { get; set; }
    public long AvailableSize { get; set; }
    public string DriveType { get; set; } = string.Empty;
    
    public string TotalSizeDisplay => FormatSize(TotalSize);
    public string AvailableSizeDisplay => FormatSize(AvailableSize);
    public string UsedSizeDisplay => FormatSize(TotalSize - AvailableSize);
    
    private static string FormatSize(long bytes)
    {
        if (bytes == 0) return "0 B";
        
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}

public class ShortcutItemInfo
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // documents, downloads, gallery, media, backup
}
