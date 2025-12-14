using QingFeng.Models;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace QingFeng.Services;

public class DiskManagementService : IDiskManagementService
{
    public async Task<List<DiskInfo>> GetAllDisksAsync()
    {
        await Task.Delay(0);
        
        var disks = new List<DiskInfo>();

        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                var diskInfo = new DiskInfo
                {
                    Name = drive.Name,
                    MountPoint = drive.RootDirectory.FullName,
                    FileSystem = drive.IsReady ? drive.DriveFormat : "Unknown",
                    IsReady = drive.IsReady
                };

                if (drive.IsReady)
                {
                    diskInfo.TotalBytes = drive.TotalSize;
                    diskInfo.AvailableBytes = drive.AvailableFreeSpace;
                    diskInfo.UsedBytes = drive.TotalSize - drive.AvailableFreeSpace;
                    diskInfo.UsagePercent = diskInfo.TotalBytes > 0 
                        ? Math.Round((double)diskInfo.UsedBytes / diskInfo.TotalBytes * 100, 2) 
                        : 0;
                }

                disks.Add(diskInfo);
            }
            catch
            {
                // Skip drives that can't be accessed
                continue;
            }
        }

        return disks;
    }

    public async Task<string> MountDiskAsync(string devicePath, string mountPoint)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return "Disk mounting is only supported on Linux";
        }

        // Validate input parameters to prevent command injection
        if (string.IsNullOrWhiteSpace(devicePath) || string.IsNullOrWhiteSpace(mountPoint))
        {
            return "Device path and mount point cannot be empty";
        }

        // Basic validation to prevent path traversal and command injection
        if (devicePath.Contains("&&") || devicePath.Contains(";") || devicePath.Contains("|") ||
            mountPoint.Contains("&&") || mountPoint.Contains(";") || mountPoint.Contains("|"))
        {
            return "Invalid characters in device path or mount point";
        }

        try
        {
            // Create mount point if it doesn't exist
            if (!Directory.Exists(mountPoint))
            {
                Directory.CreateDirectory(mountPoint);
            }

            var processInfo = new ProcessStartInfo
            {
                FileName = "mount",
                Arguments = $"{devicePath} {mountPoint}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();

                if (process.ExitCode == 0)
                {
                    return $"Successfully mounted {devicePath} to {mountPoint}";
                }
                else
                {
                    return $"Failed to mount: {error}";
                }
            }

            return "Failed to start mount process";
        }
        catch (Exception ex)
        {
            return $"Error mounting disk: {ex.Message}";
        }
    }

    public async Task<string> UnmountDiskAsync(string mountPoint)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return "Disk unmounting is only supported on Linux";
        }

        // Validate input parameter to prevent command injection
        if (string.IsNullOrWhiteSpace(mountPoint))
        {
            return "Mount point cannot be empty";
        }

        // Basic validation to prevent path traversal and command injection
        if (mountPoint.Contains("&&") || mountPoint.Contains(";") || mountPoint.Contains("|"))
        {
            return "Invalid characters in mount point";
        }

        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "umount",
                Arguments = mountPoint,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();

                if (process.ExitCode == 0)
                {
                    return $"Successfully unmounted {mountPoint}";
                }
                else
                {
                    return $"Failed to unmount: {error}";
                }
            }

            return "Failed to start umount process";
        }
        catch (Exception ex)
        {
            return $"Error unmounting disk: {ex.Message}";
        }
    }

    public async Task<List<string>> GetSharesAsync()
    {
        var shares = new List<string>();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            // Read Samba shares configuration
            try
            {
                var sambaConfigPath = "/etc/samba/smb.conf";
                if (File.Exists(sambaConfigPath))
                {
                    var lines = await File.ReadAllLinesAsync(sambaConfigPath);
                    foreach (var line in lines)
                    {
                        if (line.Trim().StartsWith("[") && line.Trim().EndsWith("]"))
                        {
                            var shareName = line.Trim().Trim('[', ']');
                            if (shareName != "global" && shareName != "homes" && shareName != "printers")
                            {
                                shares.Add(shareName);
                            }
                        }
                    }
                }
            }
            catch
            {
                // Ignore errors reading Samba config
            }
        }

        return shares;
    }
}
