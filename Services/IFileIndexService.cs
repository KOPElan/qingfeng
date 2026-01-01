using QingFeng.Models;

namespace QingFeng.Services;

public interface IFileIndexService
{
    /// <summary>
    /// Rebuilds the file index for a specified directory
    /// </summary>
    Task RebuildIndexAsync(string rootPath, IProgress<int>? progress = null);
    
    /// <summary>
    /// Search files using the index
    /// </summary>
    Task<List<FileItemInfo>> SearchIndexAsync(string searchPattern, string? rootPath = null, int maxResults = 1000);
    
    /// <summary>
    /// Get index statistics for a root path
    /// </summary>
    Task<(DateTime? lastIndexed, int fileCount)> GetIndexStatsAsync(string rootPath);
    
    /// <summary>
    /// Clear index for a specific root path
    /// </summary>
    Task ClearIndexAsync(string rootPath);
    
    /// <summary>
    /// Check if an index exists for a root path
    /// </summary>
    Task<bool> HasIndexAsync(string rootPath);
}
