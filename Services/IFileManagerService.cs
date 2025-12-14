using QingFeng.Models;

namespace QingFeng.Services;

public interface IFileManagerService
{
    Task<List<FileItemInfo>> GetFilesAsync(string path);
    Task<byte[]> ReadFileAsync(string path);
    Task WriteFileAsync(string path, byte[] content);
    Task DeleteFileAsync(string path);
    Task DeleteDirectoryAsync(string path);
    Task CreateDirectoryAsync(string path);
    Task<bool> ExistsAsync(string path);
    string GetParentPath(string path);
}
