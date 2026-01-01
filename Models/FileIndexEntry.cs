namespace QingFeng.Models;

public class FileIndexEntry
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public long Size { get; set; }
    public DateTime LastModified { get; set; }
    public string Extension { get; set; } = string.Empty;
    public DateTime IndexedAt { get; set; }
    public string RootPath { get; set; } = string.Empty; // The root directory being indexed
}
