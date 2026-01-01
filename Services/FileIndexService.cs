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

    public FileIndexService(ILogger<FileIndexService> logger)
    {
        _logger = logger;
    }

    private string GetIndexFilePath(string rootPath)
    {
        var normalizedPath = Path.GetFullPath(rootPath);
        return Path.Combine(normalizedPath, IndexFileName);
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
            
            // Build new index
            var entries = new List<FileIndexEntry>();
            var indexedAt = DateTime.UtcNow;
            var processedCount = 0;
            
            IndexDirectoryRecursive(rootPathNormalized, rootPathNormalized, entries, indexedAt, progress, ref processedCount);
            
            // Create index data structure
            var indexData = new FileIndexData
            {
                RootPath = rootPathNormalized,
                IndexedAt = indexedAt,
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

    private void IndexDirectoryRecursive(
        string currentPath, 
        string rootPath, 
        List<FileIndexEntry> entries, 
        DateTime indexedAt,
        IProgress<int>? progress,
        ref int processedCount,
        int currentDepth = 0)
    {
        if (currentDepth > MaxIndexDepth)
            return;

        try
        {
            var directory = new DirectoryInfo(currentPath);
            
            // Index files
            foreach (var file in directory.EnumerateFiles())
            {
                try
                {
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
                    if (processedCount % 100 == 0)
                    {
                        progress?.Report(processedCount);
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
                    if (processedCount % 100 == 0)
                    {
                        progress?.Report(processedCount);
                    }
                    
                    // Recursively index subdirectory
                    IndexDirectoryRecursive(subDir.FullName, rootPath, entries, indexedAt, progress, ref processedCount, currentDepth + 1);
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
    }

    public async Task<List<FileItemInfo>> SearchIndexAsync(string searchPattern, string? rootPath = null, int maxResults = 1000)
    {
        if (string.IsNullOrEmpty(rootPath))
        {
            throw new ArgumentException("必须指定根路径进行搜索", nameof(rootPath));
        }

        var normalizedRootPath = Path.GetFullPath(rootPath);
        var indexFilePath = GetIndexFilePath(normalizedRootPath);
        
        // Check if index file exists
        if (!File.Exists(indexFilePath))
        {
            throw new FileNotFoundException($"索引文件不存在，请先建立索引: {indexFilePath}");
        }
        
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
        var indexFilePath = GetIndexFilePath(normalizedRootPath);
        
        if (!File.Exists(indexFilePath))
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
        var indexFilePath = GetIndexFilePath(normalizedRootPath);
        
        return await Task.FromResult(File.Exists(indexFilePath));
    }
}
