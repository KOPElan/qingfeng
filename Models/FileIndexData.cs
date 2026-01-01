namespace QingFeng.Models;

public class FileIndexData
{
    public string RootPath { get; set; } = string.Empty;
    public DateTime IndexedAt { get; set; }
    public int TotalCount { get; set; }
    public List<FileIndexEntry> Entries { get; set; } = new();
}
