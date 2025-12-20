using QingFeng.Models;

namespace QingFeng.Services;

public class FileManagerService : IFileManagerService
{
    private readonly string _rootPath;

    public FileManagerService()
    {
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
                    if (drive.IsReady)
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

    public Task<List<ShortcutItemInfo>> GetShortcutsAsync()
    {
        var shortcuts = new List<ShortcutItemInfo>();

        try
        {
            // Add common shortcuts
            shortcuts.Add(new ShortcutItemInfo
            {
                Name = "Documents",
                Path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Icon = "description",
                Type = "documents"
            });

            shortcuts.Add(new ShortcutItemInfo
            {
                Name = "Downloads",
                Path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
                Icon = "download",
                Type = "downloads"
            });

            shortcuts.Add(new ShortcutItemInfo
            {
                Name = "Gallery",
                Path = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                Icon = "photo_library",
                Type = "gallery"
            });

            shortcuts.Add(new ShortcutItemInfo
            {
                Name = "Media",
                Path = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
                Icon = "movie",
                Type = "media"
            });
        }
        catch
        {
            // Return empty list on error
        }

        return Task.FromResult(shortcuts);
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
            // Copy file
            File.Copy(sourcePath, destinationPath, overwrite: false);
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
                catch
                {
                    // Skip files we can't access
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
                catch
                {
                    // Skip directories we can't access
                }
            }
        }
        catch
        {
            // Return empty list on error
        }

        return Task.FromResult(results);
    }

    public async Task UploadFileAsync(string directoryPath, string fileName, byte[] content)
    {
        if (!IsPathAllowed(directoryPath))
            throw new UnauthorizedAccessException("Access to this path is not allowed");

        var fullPath = Path.Combine(directoryPath, fileName);
        
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
        // Create destination directory
        Directory.CreateDirectory(destDir);

        // Copy all files
        var dir = new DirectoryInfo(sourceDir);
        foreach (var file in dir.GetFiles())
        {
            var targetPath = Path.Combine(destDir, file.Name);
            file.CopyTo(targetPath, false);
        }

        // Copy all subdirectories
        foreach (var subDir in dir.GetDirectories())
        {
            var targetPath = Path.Combine(destDir, subDir.Name);
            await CopyDirectoryAsync(subDir.FullName, targetPath);
        }
    }
}
