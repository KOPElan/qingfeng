using QingFeng.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace QingFeng.Services;

public class FileIndexService : IFileIndexService
{
    private readonly ILogger<FileIndexService> _logger;

    // Configuration constants
    private const int MaxIndexDepth = 20;
    private const string IndexFileName = ".qingfeng_index.json";
    
    // System directories to exclude from indexing (Linux)
    private static readonly HashSet<string> ExcludedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "/proc", "/sys", "/dev", "/run", "/tmp",
        "/var/run", "/var/lock", "/var/tmp",
        ".git", ".svn", ".hg", "node_modules", ".vs", "bin", "obj"
    };

    public FileIndexService(ILogger<FileIndexService> logger)
    {
        _logger = logger;
    }

    private string GetIndexFilePath(string rootPath)
    {
        var normalizedPath = Path.GetFullPath(rootPath);
        return Path.Combine(normalizedPath, IndexFileName);
    }
    
    // Find the index file by searching up the directory tree
    private string? FindIndexFile(string startPath)
    {
        var currentPath = Path.GetFullPath(startPath);
        
        // Search upwards until we find an index file or reach the root
        while (!string.IsNullOrEmpty(currentPath))
        {
            var indexFilePath = Path.Combine(currentPath, IndexFileName);
            if (File.Exists(indexFilePath))
            {
                return indexFilePath;
            }
            
            var parent = Directory.GetParent(currentPath);
            if (parent == null)
                break;
                
            currentPath = parent.FullName;
        }
        
        return null;
    }
    
    private bool ShouldExcludeDirectory(string directoryPath)
    {
        var dirName = Path.GetFileName(directoryPath);
        
        // Check if directory name matches any excluded patterns
        if (ExcludedDirectories.Contains(dirName))
            return true;
            
        // Check if it's a Linux system directory
        if (ExcludedDirectories.Contains(directoryPath))
            return true;
            
        return false;
    }

    public async Task RebuildIndexAsync(string rootPath, IProgress<int>? progress = null)
    {
        try
        {
            _logger.LogInformation("开始为 {RootPath} 重建索引", rootPath);
            
            // Validate path exists
            if (!Directory.Exists(rootPath))
            {
                throw new DirectoryNotFoundException($"目录不存在: {rootPath}");
            }

            var rootPathNormalized = Path.GetFullPath(rootPath);
            var indexFilePath = GetIndexFilePath(rootPathNormalized);
            
            // Build new index on background thread to avoid blocking UI
            var entries = await Task.Run(async () =>
            {
                var entryList = new List<FileIndexEntry>();
                var indexedAt = DateTime.UtcNow;
                var processedCount = 0;
                
                await IndexDirectoryRecursiveAsync(rootPathNormalized, rootPathNormalized, entryList, indexedAt, progress, processedCount, 0);
                
                return entryList;
            });
            
            // Create index data structure
            var indexData = new FileIndexData
            {
                RootPath = rootPathNormalized,
                IndexedAt = DateTime.UtcNow,
                TotalCount = entries.Count,
                Entries = entries
            };
            
            // Save to JSON file
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = false, // Compact format to save space
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            
            var jsonContent = JsonSerializer.Serialize(indexData, jsonOptions);
            await File.WriteAllTextAsync(indexFilePath, jsonContent);
            
            _logger.LogInformation("索引重建完成: {RootPath}, 共索引 {Count} 个文件/文件夹，已保存至 {IndexFile}", 
                rootPath, entries.Count, indexFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "重建索引失败: {RootPath}", rootPath);
            throw;
        }
    }

    private async Task<int> IndexDirectoryRecursiveAsync(
        string currentPath, 
        string rootPath, 
        List<FileIndexEntry> entries, 
        DateTime indexedAt,
        IProgress<int>? progress,
        int processedCount,
        int currentDepth = 0)
    {
        if (currentDepth > MaxIndexDepth)
            return processedCount;
        
        // Skip excluded directories
        if (ShouldExcludeDirectory(currentPath))
        {
            _logger.LogDebug("跳过排除的目录: {DirectoryPath}", currentPath);
            return processedCount;
        }

        try
        {
            var directory = new DirectoryInfo(currentPath);
            
            // Index files
            foreach (var file in directory.EnumerateFiles())
            {
                try
                {
                    // Skip hidden files except the index file itself
                    if (file.Name.StartsWith(".") && file.Name != IndexFileName)
                        continue;
                    
                    entries.Add(new FileIndexEntry
                    {
                        Name = file.Name,
                        Path = file.FullName,
                        IsDirectory = false,
                        Size = file.Length,
                        LastModified = file.LastWriteTime,
                        Extension = file.Extension.ToLowerInvariant(),
                        IndexedAt = indexedAt,
                        RootPath = rootPath
                    });
                    
                    processedCount++;
                    
                    // Report progress and yield to prevent blocking
                    if (processedCount % 50 == 0)
                    {
                        progress?.Report(processedCount);
                        await Task.Yield(); // Allow UI to update
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "无法索引文件: {FilePath}", file.FullName);
                }
            }
            
            // Index subdirectories
            foreach (var subDir in directory.EnumerateDirectories())
            {
                try
                {
                    // Skip excluded directories
                    if (ShouldExcludeDirectory(subDir.FullName))
                        continue;
                    
                    // Skip hidden directories
                    if (subDir.Name.StartsWith("."))
                        continue;
                    
                    entries.Add(new FileIndexEntry
                    {
                        Name = subDir.Name,
                        Path = subDir.FullName,
                        IsDirectory = true,
                        Size = 0,
                        LastModified = subDir.LastWriteTime,
                        Extension = string.Empty,
                        IndexedAt = indexedAt,
                        RootPath = rootPath
                    });
                    
                    processedCount++;
                    
                    // Report progress and yield to prevent blocking
                    if (processedCount % 50 == 0)
                    {
                        progress?.Report(processedCount);
                        await Task.Yield(); // Allow UI to update
                    }
                    
                    // Recursively index subdirectory
                    processedCount = await IndexDirectoryRecursiveAsync(subDir.FullName, rootPath, entries, indexedAt, progress, processedCount, currentDepth + 1);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "无法索引目录: {DirectoryPath}", subDir.FullName);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "无法访问目录: {DirectoryPath}", currentPath);
        }
        
        return processedCount;
    }

    public async Task<List<FileItemInfo>> SearchIndexAsync(string searchPattern, string? rootPath = null, int maxResults = 1000)
    {
        if (string.IsNullOrEmpty(rootPath))
        {
            throw new ArgumentException("必须指定根路径进行搜索", nameof(rootPath));
        }

        var normalizedRootPath = Path.GetFullPath(rootPath);
        
        // Try to find index file in current directory or parent directories
        var indexFilePath = FindIndexFile(normalizedRootPath);
        
        if (string.IsNullOrEmpty(indexFilePath) || !File.Exists(indexFilePath))
        {
            throw new FileNotFoundException($"未找到索引文件。请在根目录建立索引: {normalizedRootPath}");
        }
        
        _logger.LogDebug("使用索引文件: {IndexFile} 搜索目录: {SearchPath}", indexFilePath, normalizedRootPath);
        
        // Load index from file
        var jsonContent = await File.ReadAllTextAsync(indexFilePath);
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        
        var indexData = JsonSerializer.Deserialize<FileIndexData>(jsonContent, jsonOptions);
        if (indexData == null || indexData.Entries == null)
        {
            throw new InvalidOperationException("索引文件格式无效");
        }
        
        // Filter entries based on search pattern
        IEnumerable<FileIndexEntry> filteredEntries = indexData.Entries;
        
        // If searching in a subdirectory, filter to only entries within that subdirectory
        if (normalizedRootPath != indexData.RootPath)
        {
            filteredEntries = filteredEntries.Where(e => e.Path.StartsWith(normalizedRootPath, StringComparison.OrdinalIgnoreCase));
        }
        
        if (searchPattern.Contains('*') || searchPattern.Contains('?'))
        {
            // Convert wildcard pattern to regex pattern
            var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(searchPattern)
                .Replace("\\*", ".*")
                .Replace("\\?", ".") + "$";
            var regex = new System.Text.RegularExpressions.Regex(regexPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            filteredEntries = filteredEntries.Where(e => regex.IsMatch(e.Name));
        }
        else
        {
            // Simple contains search (case-insensitive)
            filteredEntries = filteredEntries.Where(e => e.Name.Contains(searchPattern, StringComparison.OrdinalIgnoreCase));
        }
        
        // Convert to FileItemInfo and apply limit
        var results = filteredEntries
            .OrderBy(e => e.Name)
            .Take(maxResults)
            .Select(e => new FileItemInfo
            {
                Name = e.Name,
                Path = e.Path,
                IsDirectory = e.IsDirectory,
                Size = e.Size,
                LastModified = e.LastModified,
                Extension = e.Extension
            })
            .ToList();
        
        return results;
    }

    public async Task<(DateTime? lastIndexed, int fileCount)> GetIndexStatsAsync(string rootPath)
    {
        var normalizedRootPath = Path.GetFullPath(rootPath);
        
        // Try to find index file in current directory or parent directories
        var indexFilePath = FindIndexFile(normalizedRootPath);
        
        if (string.IsNullOrEmpty(indexFilePath) || !File.Exists(indexFilePath))
        {
            return (null, 0);
        }
        
        try
        {
            var jsonContent = await File.ReadAllTextAsync(indexFilePath);
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            
            var indexData = JsonSerializer.Deserialize<FileIndexData>(jsonContent, jsonOptions);
            if (indexData == null)
            {
                return (null, 0);
            }
            
            return (indexData.IndexedAt, indexData.TotalCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "读取索引文件失败: {IndexFile}", indexFilePath);
            return (null, 0);
        }
    }

    public async Task ClearIndexAsync(string rootPath)
    {
        var normalizedRootPath = Path.GetFullPath(rootPath);
        var indexFilePath = GetIndexFilePath(normalizedRootPath);
        
        if (File.Exists(indexFilePath))
        {
            File.Delete(indexFilePath);
            _logger.LogInformation("已删除索引文件: {IndexFile}", indexFilePath);
        }
        
        await Task.CompletedTask;
    }

    public async Task<bool> HasIndexAsync(string rootPath)
    {
        var normalizedRootPath = Path.GetFullPath(rootPath);
        
        // Try to find index file in current directory or parent directories
        var indexFilePath = FindIndexFile(normalizedRootPath);
        
        return await Task.FromResult(!string.IsNullOrEmpty(indexFilePath) && File.Exists(indexFilePath));
    }
}
