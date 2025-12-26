using QingFeng.Models;
using QingFeng.Data;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;

namespace QingFeng.Services;

public class FileManagerService : IFileManagerService
{
    private readonly string _rootPath;
    private readonly ILogger<FileManagerService> _logger;
    private readonly IDbContextFactory<QingFengDbContext> _dbContextFactory;
    
    // Static HashSet to avoid recreating on every drive check
    private static readonly HashSet<string> ExcludedLinuxPaths = new()
    {
        "/proc", "/sys", "/dev", "/run",
        "/sys/kernel/security", "/dev/shm", "/dev/pts",
        "/run/lock", "/sys/fs/cgroup", "/sys/fs/pstore",
        "/sys/fs/bpf", "/proc/sys/fs/binfmt_misc",
        "/dev/hugepages", "/dev/mqueue", "/sys/kernel/debug",
        "/sys/kernel/tracing", "/sys/fs/fuse/connections",
        "/sys/kernel/config"
    };

    public FileManagerService(ILogger<FileManagerService> logger, IDbContextFactory<QingFengDbContext> dbContextFactory)
    {
        _logger = logger;
        _dbContextFactory = dbContextFactory;
        
        // Set root path based on OS
        if (OperatingSystem.IsWindows())
        {
            _rootPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }
        else
        {
            _rootPath = "/";
        }
    }

    public Task<List<FileItemInfo>> GetFilesAsync(string path)
    {
        var files = new List<FileItemInfo>();
        
        try
        {
            // Validate path
            if (string.IsNullOrWhiteSpace(path))
                path = _rootPath;

            var fullPath = Path.GetFullPath(path);
            
            // Security check: ensure path is within allowed root
            if (!IsPathAllowed(fullPath))
                return Task.FromResult(files);

            var directory = new DirectoryInfo(fullPath);
            
            if (!directory.Exists)
                return Task.FromResult(files);

            // Add parent directory link if not at root
            if (directory.Parent != null)
            {
                files.Add(new FileItemInfo
                {
                    Name = "..",
                    Path = directory.Parent.FullName,
                    IsDirectory = true,
                    LastModified = directory.LastWriteTime
                });
            }

            // Add directories
            foreach (var dir in directory.GetDirectories().OrderBy(d => d.Name))
            {
                try
                {
                    files.Add(new FileItemInfo
                    {
                        Name = dir.Name,
                        Path = dir.FullName,
                        IsDirectory = true,
                        LastModified = dir.LastWriteTime
                    });
                }
                catch
                {
                    // Skip directories we can't access
                }
            }

            // Add files
            foreach (var file in directory.GetFiles().OrderBy(f => f.Name))
            {
                try
                {
                    files.Add(new FileItemInfo
                    {
                        Name = file.Name,
                        Path = file.FullName,
                        IsDirectory = false,
                        Size = file.Length,
                        LastModified = file.LastWriteTime,
                        Extension = file.Extension
                    });
                }
                catch
                {
                    // Skip files we can't access
                }
            }
        }
        catch
        {
            // Return empty list on error
        }

        return Task.FromResult(files);
    }

    public async Task<byte[]> ReadFileAsync(string path)
    {
        if (!IsPathAllowed(path))
            throw new UnauthorizedAccessException("Access to this path is not allowed");

        return await File.ReadAllBytesAsync(path);
    }

    public async Task WriteFileAsync(string path, byte[] content)
    {
        if (!IsPathAllowed(path))
            throw new UnauthorizedAccessException("Access to this path is not allowed");

        await File.WriteAllBytesAsync(path, content);
    }

    public Task DeleteFileAsync(string path)
    {
        if (!IsPathAllowed(path))
            throw new UnauthorizedAccessException("Access to this path is not allowed");

        File.Delete(path);
        return Task.CompletedTask;
    }

    public Task DeleteDirectoryAsync(string path)
    {
        if (!IsPathAllowed(path))
            throw new UnauthorizedAccessException("Access to this path is not allowed");

        Directory.Delete(path, true);
        return Task.CompletedTask;
    }

    public Task CreateDirectoryAsync(string path)
    {
        if (!IsPathAllowed(path))
            throw new UnauthorizedAccessException("Access to this path is not allowed");

        Directory.CreateDirectory(path);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string path)
    {
        if (!IsPathAllowed(path))
            return Task.FromResult(false);

        return Task.FromResult(File.Exists(path) || Directory.Exists(path));
    }

    public string GetParentPath(string path)
    {
        try
        {
            var directory = new DirectoryInfo(path);
            return directory.Parent?.FullName ?? path;
        }
        catch
        {
            return path;
        }
    }

    private bool IsPathAllowed(string path)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            var rootPath = Path.GetFullPath(_rootPath);
            
            // On Linux/Unix, allow access to root and its subdirectories
            if (!OperatingSystem.IsWindows())
            {
                return fullPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase);
            }
            
            // On Windows, allow access to user profile and its subdirectories
            return fullPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public Task<List<DriveItemInfo>> GetDrivesAsync()
    {
        var drives = new List<DriveItemInfo>();

        try
        {
            var driveInfos = DriveInfo.GetDrives();
            foreach (var drive in driveInfos)
            {
                try
                {
                    if (drive.IsReady && IsUserAccessibleDrive(drive))
                    {
                        drives.Add(new DriveItemInfo
                        {
                            Name = string.IsNullOrWhiteSpace(drive.VolumeLabel) ? drive.Name : drive.VolumeLabel,
                            Path = drive.RootDirectory.FullName,
                            TotalSize = drive.TotalSize,
                            AvailableSize = drive.AvailableFreeSpace,
                            DriveType = drive.DriveType.ToString(),
                            Icon = drive.DriveType switch
                            {
                                System.IO.DriveType.Fixed => "storage",
                                System.IO.DriveType.Removable => "usb",
                                System.IO.DriveType.Network => "cloud",
                                _ => "folder"
                            }
                        });
                    }
                }
                catch
                {
                    // Skip drives we can't access
                }
            }
        }
        catch
        {
            // Return empty list on error
        }

        return Task.FromResult(drives);
    }
    
    private bool IsUserAccessibleDrive(DriveInfo drive)
    {
        // On Linux, filter out virtual and system filesystems that users don't need to access
        if (!OperatingSystem.IsWindows())
        {
            var path = drive.RootDirectory.FullName;
            
            // Exclude exact matches
            if (ExcludedLinuxPaths.Contains(path))
            {
                return false;
            }
            
            // Exclude paths that are subdirectories of excluded paths
            foreach (var excluded in ExcludedLinuxPaths)
            {
                if (path.StartsWith(excluded + "/"))
                {
                    return false;
                }
            }
            
            // Only include Fixed, Removable, and Network drives
            if (drive.DriveType != System.IO.DriveType.Fixed && 
                drive.DriveType != System.IO.DriveType.Removable && 
                drive.DriveType != System.IO.DriveType.Network)
            {
                return false;
            }
        }
        
        return true;
    }    

    public Task<(long total, long available)> GetStorageInfoAsync(string path)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            
            // Find the drive for this path
            var driveInfos = DriveInfo.GetDrives();
            foreach (var drive in driveInfos)
            {
                if (drive.IsReady && fullPath.StartsWith(drive.RootDirectory.FullName, StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult((drive.TotalSize, drive.AvailableFreeSpace));
                }
            }
        }
        catch
        {
            // Return zeros on error
        }

        return Task.FromResult((0L, 0L));
    }

    public Task RenameAsync(string oldPath, string newPath)
    {
        if (!IsPathAllowed(oldPath) || !IsPathAllowed(newPath))
            throw new UnauthorizedAccessException("Access to this path is not allowed");

        if (File.Exists(oldPath))
        {
            File.Move(oldPath, newPath);
        }
        else if (Directory.Exists(oldPath))
        {
            Directory.Move(oldPath, newPath);
        }
        else
        {
            throw new FileNotFoundException("Source file or directory not found", oldPath);
        }

        return Task.CompletedTask;
    }

    public async Task CopyAsync(string sourcePath, string destinationPath)
    {
        if (!IsPathAllowed(sourcePath) || !IsPathAllowed(destinationPath))
            throw new UnauthorizedAccessException("Access to this path is not allowed");

        if (File.Exists(sourcePath))
        {
            // Copy file - if destination exists, generate a new name
            var finalDestPath = destinationPath;
            if (File.Exists(finalDestPath))
            {
                var directory = Path.GetDirectoryName(finalDestPath) ?? string.Empty;
                var fileName = Path.GetFileNameWithoutExtension(finalDestPath);
                var extension = Path.GetExtension(finalDestPath);
                var counter = 1;
                
                do
                {
                    finalDestPath = Path.Combine(directory, $"{fileName} ({counter}){extension}");
                    counter++;
                } while (File.Exists(finalDestPath));
            }
            
            // Validate the final path after conflict resolution
            if (!IsPathAllowed(finalDestPath))
                throw new UnauthorizedAccessException("Access to this path is not allowed");
            
            File.Copy(sourcePath, finalDestPath, overwrite: false);
        }
        else if (Directory.Exists(sourcePath))
        {
            // Copy directory recursively
            await CopyDirectoryAsync(sourcePath, destinationPath);
        }
        else
        {
            throw new FileNotFoundException("Source file or directory not found", sourcePath);
        }
    }

    public Task MoveAsync(string sourcePath, string destinationPath)
    {
        if (!IsPathAllowed(sourcePath) || !IsPathAllowed(destinationPath))
            throw new UnauthorizedAccessException("Access to this path is not allowed");

        // Check if destination already exists
        if (File.Exists(destinationPath) || Directory.Exists(destinationPath))
            throw new IOException($"Destination already exists: {Path.GetFileName(destinationPath)}");

        if (File.Exists(sourcePath))
        {
            File.Move(sourcePath, destinationPath);
        }
        else if (Directory.Exists(sourcePath))
        {
            Directory.Move(sourcePath, destinationPath);
        }
        else
        {
            throw new FileNotFoundException("Source file or directory not found", sourcePath);
        }

        return Task.CompletedTask;
    }

    public Task<List<FileItemInfo>> SearchFilesAsync(string path, string searchPattern)
    {
        var results = new List<FileItemInfo>();

        try
        {
            if (!IsPathAllowed(path))
                return Task.FromResult(results);

            var directory = new DirectoryInfo(path);
            if (!directory.Exists)
                return Task.FromResult(results);

            // Search for files matching pattern
            var files = directory.GetFiles(searchPattern, SearchOption.AllDirectories);
            foreach (var file in files)
            {
                try
                {
                    results.Add(new FileItemInfo
                    {
                        Name = file.Name,
                        Path = file.FullName,
                        IsDirectory = false,
                        Size = file.Length,
                        LastModified = file.LastWriteTime,
                        Extension = file.Extension
                    });
                }
                catch (Exception ex)
                {
                    // Skip files we can't access, but log for diagnostics
                    _logger.LogDebug(ex, "Failed to access file '{FilePath}' during search", file.FullName);
                }
            }

            // Search for directories matching pattern
            var directories = directory.GetDirectories(searchPattern, SearchOption.AllDirectories);
            foreach (var dir in directories)
            {
                try
                {
                    results.Add(new FileItemInfo
                    {
                        Name = dir.Name,
                        Path = dir.FullName,
                        IsDirectory = true,
                        LastModified = dir.LastWriteTime
                    });
                }
                catch (Exception ex)
                {
                    // Skip directories we can't access, but log for diagnostics
                    _logger.LogDebug(ex, "Failed to access directory '{DirectoryPath}' during search", dir.FullName);
                }
            }
        }
        catch (Exception ex)
        {
            // Return empty list on error, but log for diagnostics
            _logger.LogWarning(ex, "Error while searching files in '{Path}' with pattern '{SearchPattern}'", path, searchPattern);
        }

        return Task.FromResult(results);
    }

    public async Task UploadFileAsync(string directoryPath, string fileName, byte[] content)
    {
        if (!IsPathAllowed(directoryPath))
            throw new UnauthorizedAccessException("Access to this path is not allowed");

        // Sanitize filename to prevent path traversal attacks
        var sanitizedFileName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(sanitizedFileName) || 
            sanitizedFileName.Contains("..") || 
            sanitizedFileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException("Invalid file name", nameof(fileName));
        }

        var fullPath = Path.Combine(directoryPath, sanitizedFileName);
        
        if (!IsPathAllowed(fullPath))
            throw new UnauthorizedAccessException("Access to this path is not allowed");

        await File.WriteAllBytesAsync(fullPath, content);
    }

    public async Task<byte[]> DownloadFileAsync(string filePath)
    {
        if (!IsPathAllowed(filePath))
            throw new UnauthorizedAccessException("Access to this path is not allowed");

        if (!File.Exists(filePath))
            throw new FileNotFoundException("File not found", filePath);

        return await File.ReadAllBytesAsync(filePath);
    }

    private async Task CopyDirectoryAsync(string sourceDir, string destDir)
    {
        // Validate destination directory path
        if (!IsPathAllowed(destDir))
            throw new UnauthorizedAccessException("Access to this path is not allowed");

        // Create destination directory
        Directory.CreateDirectory(destDir);

        // Copy all files
        var dir = new DirectoryInfo(sourceDir);
        foreach (var file in dir.GetFiles())
        {
            var targetPath = Path.Combine(destDir, file.Name);
            
            // Handle file conflicts by generating a new name
            if (File.Exists(targetPath))
            {
                var fileName = Path.GetFileNameWithoutExtension(file.Name);
                var extension = Path.GetExtension(file.Name);
                var counter = 1;
                
                do
                {
                    targetPath = Path.Combine(destDir, $"{fileName} ({counter}){extension}");
                    counter++;
                } while (File.Exists(targetPath));
            }
            
            // Validate the final target path after conflict resolution
            if (!IsPathAllowed(targetPath))
                throw new UnauthorizedAccessException("Access to this path is not allowed");
            
            file.CopyTo(targetPath, false);
        }

        // Copy all subdirectories
        foreach (var subDir in dir.GetDirectories())
        {
            var targetPath = Path.Combine(destDir, subDir.Name);
            // Recursive call will validate the subdirectory path
            await CopyDirectoryAsync(subDir.FullName, targetPath);
        }
    }

    // Favorites management
    public async Task<List<FavoriteFolder>> GetFavoriteFoldersAsync()
    {
        using var context = await _dbContextFactory.CreateDbContextAsync();
        return await context.FavoriteFolders
            .OrderBy(f => f.Order)
            .ThenBy(f => f.Name)
            .ToListAsync();
    }

    public async Task<FavoriteFolder> AddFavoriteFolderAsync(string name, string path, string icon = "folder")
    {
        ValidateFavoriteInputs(name, icon);

        if (!IsPathAllowed(path))
            throw new UnauthorizedAccessException("Access to this path is not allowed");

        if (!Directory.Exists(path))
            throw new DirectoryNotFoundException($"Directory not found: {path}");

        using var context = await _dbContextFactory.CreateDbContextAsync();
        
        // Get next order value
        var maxOrder = await context.FavoriteFolders.MaxAsync(f => (int?)f.Order) ?? 0;

        var favorite = new FavoriteFolder
        {
            Name = name,
            Path = path,
            Icon = icon,
            Order = maxOrder + 1
        };

        context.FavoriteFolders.Add(favorite);
        
        try
        {
            await context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException sqliteEx && sqliteEx.SqliteErrorCode == 19)
        {
            // SQLite error code 19 is SQLITE_CONSTRAINT (unique constraint violation)
            throw new InvalidOperationException("This folder is already in favorites");
        }

        return favorite;
    }

    public async Task RemoveFavoriteFolderAsync(int id)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync();
        
        var favorite = await context.FavoriteFolders.FindAsync(id);
        if (favorite == null)
            throw new InvalidOperationException("Favorite folder not found");

        context.FavoriteFolders.Remove(favorite);
        await context.SaveChangesAsync();
    }

    public async Task UpdateFavoriteFolderAsync(int id, string name, string icon)
    {
        ValidateFavoriteInputs(name, icon);

        using var context = await _dbContextFactory.CreateDbContextAsync();
        
        var favorite = await context.FavoriteFolders.FindAsync(id);
        if (favorite == null)
            throw new InvalidOperationException("Favorite folder not found");

        favorite.Name = name;
        favorite.Icon = icon;

        await context.SaveChangesAsync();
    }

    private void ValidateFavoriteInputs(string name, string icon)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty", nameof(name));
        
        if (name.Length > 200)
            throw new ArgumentException("Name cannot exceed 200 characters", nameof(name));
        
        if (string.IsNullOrWhiteSpace(icon))
            throw new ArgumentException("Icon cannot be empty", nameof(icon));
        
        if (icon.Length > 100)
            throw new ArgumentException("Icon cannot exceed 100 characters", nameof(icon));
    }

    // Batch operations
    public async Task BatchCopyAsync(List<string> sourcePaths, string destinationPath)
    {
        if (!IsPathAllowed(destinationPath))
            throw new UnauthorizedAccessException("Access to destination path is not allowed");

        if (!Directory.Exists(destinationPath))
            throw new DirectoryNotFoundException($"Destination directory not found: {destinationPath}");

        var errors = new List<string>();
        
        foreach (var sourcePath in sourcePaths)
        {
            try
            {
                if (!IsPathAllowed(sourcePath))
                {
                    errors.Add($"{Path.GetFileName(sourcePath)}: Access denied");
                    continue;
                }

                var fileName = Path.GetFileName(sourcePath);
                var destPath = Path.Combine(destinationPath, fileName);
                
                await CopyAsync(sourcePath, destPath);
            }
            catch (Exception ex)
            {
                errors.Add($"{Path.GetFileName(sourcePath)}: {ex.Message}");
            }
        }

        if (errors.Any())
        {
            throw new AggregateException($"Some files failed to copy:\n{string.Join("\n", errors)}");
        }
    }

    public async Task BatchMoveAsync(List<string> sourcePaths, string destinationPath)
    {
        if (!IsPathAllowed(destinationPath))
            throw new UnauthorizedAccessException("Access to destination path is not allowed");

        if (!Directory.Exists(destinationPath))
            throw new DirectoryNotFoundException($"Destination directory not found: {destinationPath}");

        var errors = new List<string>();
        
        foreach (var sourcePath in sourcePaths)
        {
            try
            {
                if (!IsPathAllowed(sourcePath))
                {
                    errors.Add($"{Path.GetFileName(sourcePath)}: Access denied");
                    continue;
                }

                var fileName = Path.GetFileName(sourcePath);
                var destPath = Path.Combine(destinationPath, fileName);
                
                // Check if destination already exists
                if (File.Exists(destPath) || Directory.Exists(destPath))
                {
                    errors.Add($"{fileName}: Destination already exists");
                    continue;
                }
                
                await MoveAsync(sourcePath, destPath);
            }
            catch (Exception ex)
            {
                errors.Add($"{Path.GetFileName(sourcePath)}: {ex.Message}");
            }
        }

        if (errors.Any())
        {
            throw new AggregateException($"Some files failed to move:\n{string.Join("\n", errors)}");
        }
    }

    public Task BatchDeleteAsync(List<string> paths)
    {
        var errors = new List<string>();
        
        foreach (var path in paths)
        {
            try
            {
                if (!IsPathAllowed(path))
                {
                    errors.Add($"{Path.GetFileName(path)}: Access denied");
                    continue;
                }

                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                else if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
                else
                {
                    errors.Add($"{Path.GetFileName(path)}: Not found");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"{Path.GetFileName(path)}: {ex.Message}");
            }
        }

        if (errors.Any())
        {
            throw new AggregateException($"Some files failed to delete:\n{string.Join("\n", errors)}");
        }

        return Task.CompletedTask;
    }
}
