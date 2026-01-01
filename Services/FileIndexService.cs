using QingFeng.Models;
using QingFeng.Data;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace QingFeng.Services;

public class FileIndexService : IFileIndexService
{
    private readonly ILogger<FileIndexService> _logger;
    private readonly IDbContextFactory<QingFengDbContext> _dbContextFactory;

    public FileIndexService(ILogger<FileIndexService> logger, IDbContextFactory<QingFengDbContext> dbContextFactory)
    {
        _logger = logger;
        _dbContextFactory = dbContextFactory;
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
            
            using var context = await _dbContextFactory.CreateDbContextAsync();
            
            // Clear existing index for this root path
            await context.FileIndexEntries
                .Where(e => e.RootPath == rootPathNormalized)
                .ExecuteDeleteAsync();
            
            // Build new index
            var entries = new List<FileIndexEntry>();
            var indexedAt = DateTime.UtcNow;
            var processedCount = 0;
            
            IndexDirectoryRecursive(rootPathNormalized, rootPathNormalized, entries, indexedAt, progress, ref processedCount);
            
            // Save in batches for better performance
            const int batchSize = 1000;
            for (int i = 0; i < entries.Count; i += batchSize)
            {
                var batch = entries.Skip(i).Take(batchSize).ToList();
                await context.FileIndexEntries.AddRangeAsync(batch);
                await context.SaveChangesAsync();
            }
            
            _logger.LogInformation("索引重建完成: {RootPath}, 共索引 {Count} 个文件/文件夹", rootPath, entries.Count);
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
        int maxDepth = 20,
        int currentDepth = 0)
    {
        if (currentDepth > maxDepth)
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
                    IndexDirectoryRecursive(subDir.FullName, rootPath, entries, indexedAt, progress, ref processedCount, maxDepth, currentDepth + 1);
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
        using var context = await _dbContextFactory.CreateDbContextAsync();
        
        var query = context.FileIndexEntries.AsQueryable();
        
        // Filter by root path if specified
        if (!string.IsNullOrEmpty(rootPath))
        {
            var normalizedRootPath = Path.GetFullPath(rootPath);
            query = query.Where(e => e.RootPath == normalizedRootPath);
        }
        
        // Apply search pattern
        // Support wildcards like *.txt or partial matches
        if (searchPattern.Contains('*') || searchPattern.Contains('?'))
        {
            // Convert wildcard pattern to regex-like pattern for EF Core
            var pattern = searchPattern.Replace("*", "%").Replace("?", "_");
            query = query.Where(e => EF.Functions.Like(e.Name, pattern));
        }
        else
        {
            // Simple contains search
            query = query.Where(e => e.Name.Contains(searchPattern));
        }
        
        var results = await query
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
            .ToListAsync();
        
        return results;
    }

    public async Task<(DateTime? lastIndexed, int fileCount)> GetIndexStatsAsync(string rootPath)
    {
        var normalizedRootPath = Path.GetFullPath(rootPath);
        
        using var context = await _dbContextFactory.CreateDbContextAsync();
        
        var stats = await context.FileIndexEntries
            .Where(e => e.RootPath == normalizedRootPath)
            .GroupBy(e => e.RootPath)
            .Select(g => new
            {
                LastIndexed = g.Max(e => e.IndexedAt),
                FileCount = g.Count()
            })
            .FirstOrDefaultAsync();
        
        if (stats == null)
            return (null, 0);
        
        return (stats.LastIndexed, stats.FileCount);
    }

    public async Task ClearIndexAsync(string rootPath)
    {
        var normalizedRootPath = Path.GetFullPath(rootPath);
        
        using var context = await _dbContextFactory.CreateDbContextAsync();
        
        await context.FileIndexEntries
            .Where(e => e.RootPath == normalizedRootPath)
            .ExecuteDeleteAsync();
    }

    public async Task<bool> HasIndexAsync(string rootPath)
    {
        var normalizedRootPath = Path.GetFullPath(rootPath);
        
        using var context = await _dbContextFactory.CreateDbContextAsync();
        
        return await context.FileIndexEntries
            .AnyAsync(e => e.RootPath == normalizedRootPath);
    }
}
