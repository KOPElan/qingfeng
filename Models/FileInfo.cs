using QingFeng.Utilities;

namespace QingFeng.Models;

public class FileItemInfo
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public long Size { get; set; }
    public DateTime LastModified { get; set; }
    public string Extension { get; set; } = string.Empty;
    
    public string SizeDisplay => IsDirectory ? "-" : FileUtilities.FormatSize(Size);
}
