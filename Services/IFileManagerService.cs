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
    Task<List<DriveItemInfo>> GetDrivesAsync();    
    Task<(long total, long available)> GetStorageInfoAsync(string path);
    
    // New methods for complete file management
    Task RenameAsync(string oldPath, string newPath);
    Task CopyAsync(string sourcePath, string destinationPath);
    Task MoveAsync(string sourcePath, string destinationPath);
    Task<List<FileItemInfo>> SearchFilesAsync(string path, string searchPattern, int maxResults = 1000, int maxDepth = 10);
    Task UploadFileAsync(string directoryPath, string fileName, byte[] content);
    Task UploadFileStreamAsync(string directoryPath, string fileName, Stream fileStream, long fileSize);
    Task<byte[]> DownloadFileAsync(string filePath);
    
    // Favorites management
    Task<List<FavoriteFolder>> GetFavoriteFoldersAsync();
    Task<FavoriteFolder> AddFavoriteFolderAsync(string name, string path, string icon = "folder");
    Task RemoveFavoriteFolderAsync(int id);
    Task UpdateFavoriteFolderAsync(int id, string name, string icon);
    
    // Batch operations
    Task BatchCopyAsync(List<string> sourcePaths, string destinationPath);
    Task BatchMoveAsync(List<string> sourcePaths, string destinationPath);
    Task BatchDeleteAsync(List<string> paths);
}
