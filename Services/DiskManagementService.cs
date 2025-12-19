using QingFeng.Models;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace QingFeng.Services;

public class DiskManagementService : IDiskManagementService
{
    private static readonly string[] InvalidChars = ["&&", ";", "|", "`", "$", "(", ")", "<", ">", "\"", "'", "\n", "\r"];

    public Task<List<DiskInfo>> GetAllDisksAsync()
    {
        var disks = new List<DiskInfo>();

        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                var diskInfo = new DiskInfo
                {
                    Name = drive.Name,
                    DevicePath = drive.Name,
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

        return Task.FromResult(disks);
    }

    public async Task<List<DiskInfo>> GetAllBlockDevicesAsync()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            // Fall back to basic disk info on non-Linux systems
            return await GetAllDisksAsync();
        }

        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "lsblk",
                Arguments = "-J -b -o NAME,TYPE,SIZE,MOUNTPOINT,FSTYPE,UUID,LABEL,RM,RO,MODEL,SERIAL",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process == null)
            {
                return await GetAllDisksAsync();
            }

            await process.WaitForExitAsync();
            var output = await process.StandardOutput.ReadToEndAsync();

            if (process.ExitCode != 0)
            {
                return await GetAllDisksAsync();
            }

            var lsblkData = JsonSerializer.Deserialize<LsblkOutput>(output);
            if (lsblkData?.Blockdevices == null)
            {
                return await GetAllDisksAsync();
            }

            var disks = new List<DiskInfo>();
            foreach (var device in lsblkData.Blockdevices)
            {
                var diskInfo = ConvertLsblkDevice(device);
                disks.Add(diskInfo);
            }

            // Get usage information for mounted disks
            await EnrichWithUsageInfo(disks);

            return disks;
        }
        catch
        {
            return await GetAllDisksAsync();
        }
    }

    private DiskInfo ConvertLsblkDevice(LsblkDevice device)
    {
        var diskInfo = new DiskInfo
        {
            Name = device.Name ?? "",
            DevicePath = $"/dev/{device.Name}",
            Type = device.Type ?? "",
            TotalBytes = device.Size,
            FileSystem = device.Fstype ?? "",
            UUID = device.Uuid ?? "",
            Label = device.Label ?? "",
            IsRemovable = device.Rm,
            IsReadOnly = device.Ro,
            Model = device.Model ?? "",
            Serial = device.Serial ?? "",
            MountPoint = device.Mountpoint ?? "",
            IsReady = !string.IsNullOrEmpty(device.Mountpoint)
        };

        if (device.Children != null)
        {
            foreach (var child in device.Children)
            {
                diskInfo.Children.Add(ConvertLsblkDevice(child));
            }
        }

        return diskInfo;
    }

    private async Task EnrichWithUsageInfo(List<DiskInfo> disks)
    {
        foreach (var disk in disks)
        {
            if (!string.IsNullOrEmpty(disk.MountPoint) && Directory.Exists(disk.MountPoint))
            {
                try
                {
                    var driveInfo = new DriveInfo(disk.MountPoint);
                    if (driveInfo.IsReady)
                    {
                        disk.TotalBytes = driveInfo.TotalSize;
                        disk.AvailableBytes = driveInfo.AvailableFreeSpace;
                        disk.UsedBytes = driveInfo.TotalSize - driveInfo.AvailableFreeSpace;
                        disk.UsagePercent = disk.TotalBytes > 0
                            ? Math.Round((double)disk.UsedBytes / disk.TotalBytes * 100, 2)
                            : 0;
                    }
                }
                catch
                {
                    // Ignore errors getting usage info
                }
            }

            if (disk.Children.Count > 0)
            {
                await EnrichWithUsageInfo(disk.Children);
            }
        }
    }

    public async Task<DiskInfo?> GetDiskInfoAsync(string devicePath)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return null;
        }

        if (!ValidateDevicePath(devicePath))
        {
            return null;
        }

        var allDisks = await GetAllBlockDevicesAsync();
        return FindDiskByPath(allDisks, devicePath);
    }

    private DiskInfo? FindDiskByPath(List<DiskInfo> disks, string devicePath)
    {
        foreach (var disk in disks)
        {
            if (disk.DevicePath == devicePath)
            {
                return disk;
            }

            if (disk.Children.Count > 0)
            {
                var found = FindDiskByPath(disk.Children, devicePath);
                if (found != null)
                {
                    return found;
                }
            }
        }

        return null;
    }

    public async Task<string> MountDiskAsync(string devicePath, string mountPoint, string? fileSystem = null, string? options = null)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return "Disk mounting is only supported on Linux";
        }

        if (!ValidateDevicePath(devicePath))
        {
            return "Invalid device path";
        }

        if (!ValidateMountPoint(mountPoint))
        {
            return "Invalid mount point";
        }

        if (options != null && InvalidChars.Any(c => options.Contains(c)))
        {
            return "Invalid characters in mount options";
        }

        try
        {
            // Create mount point if it doesn't exist
            if (!Directory.Exists(mountPoint))
            {
                Directory.CreateDirectory(mountPoint);
            }

            var arguments = devicePath + " " + mountPoint;
            if (!string.IsNullOrEmpty(fileSystem))
            {
                arguments = $"-t {fileSystem} {arguments}";
            }
            if (!string.IsNullOrEmpty(options))
            {
                arguments = $"-o {options} {arguments}";
            }

            var processInfo = new ProcessStartInfo
            {
                FileName = "mount",
                Arguments = arguments,
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

    public async Task<string> MountDiskPermanentAsync(string devicePath, string mountPoint, string? fileSystem = null, string? options = null)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return "Permanent disk mounting is only supported on Linux";
        }

        // First mount temporarily
        var mountResult = await MountDiskAsync(devicePath, mountPoint, fileSystem, options);
        if (!mountResult.Contains("Successfully"))
        {
            return mountResult;
        }

        try
        {
            // Get disk info to find UUID
            var diskInfo = await GetDiskInfoAsync(devicePath);
            if (diskInfo == null)
            {
                return "Could not get disk information";
            }

            var fstabEntry = string.IsNullOrEmpty(diskInfo.UUID)
                ? devicePath
                : $"UUID={diskInfo.UUID}";

            fstabEntry += $" {mountPoint}";
            fstabEntry += $" {(string.IsNullOrEmpty(fileSystem) ? (diskInfo.FileSystem ?? "auto") : fileSystem)}";
            fstabEntry += $" {(string.IsNullOrEmpty(options) ? "defaults" : options)}";
            fstabEntry += " 0 2";

            // Append to /etc/fstab
            var processInfo = new ProcessStartInfo
            {
                FileName = "bash",
                Arguments = $"-c \"echo '{fstabEntry}' >> /etc/fstab\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
                var error = await process.StandardError.ReadToEndAsync();

                if (process.ExitCode == 0)
                {
                    return $"Successfully mounted {devicePath} to {mountPoint} and added to /etc/fstab";
                }
                else
                {
                    return $"Mounted successfully but failed to update /etc/fstab: {error}. Entry: {fstabEntry}";
                }
            }

            return "Failed to update /etc/fstab";
        }
        catch (Exception ex)
        {
            return $"Mounted successfully but error updating /etc/fstab: {ex.Message}";
        }
    }

    public async Task<string> UnmountDiskAsync(string mountPoint)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return "Disk unmounting is only supported on Linux";
        }

        if (!ValidateMountPoint(mountPoint))
        {
            return "Invalid mount point";
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

    public Task<List<string>> GetAvailableFileSystemsAsync()
    {
        // Common Linux file systems
        var fileSystems = new List<string>
        {
            "ext4",
            "ext3",
            "ext2",
            "btrfs",
            "xfs",
            "ntfs",
            "vfat",
            "exfat"
        };

        return Task.FromResult(fileSystems);
    }

    public async Task<string> SetDiskSpinDownAsync(string devicePath, int timeoutMinutes)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return "Disk power management is only supported on Linux";
        }

        if (!ValidateDevicePath(devicePath))
        {
            return "Invalid device path";
        }

        if (timeoutMinutes < 0 || timeoutMinutes > 240)
        {
            return "Timeout must be between 0 and 240 minutes";
        }

        try
        {
            // Convert minutes to hdparm units (5 seconds per unit)
            var hdparmValue = timeoutMinutes == 0 ? "0" : (timeoutMinutes * 12).ToString();

            var processInfo = new ProcessStartInfo
            {
                FileName = "hdparm",
                Arguments = $"-S {hdparmValue} {devicePath}",
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
                    var msg = timeoutMinutes == 0
                        ? $"Disabled spin-down for {devicePath}"
                        : $"Set spin-down timeout to {timeoutMinutes} minutes for {devicePath}";
                    return msg;
                }
                else
                {
                    return $"Failed to set spin-down: {error}";
                }
            }

            return "Failed to start hdparm process";
        }
        catch (Exception ex)
        {
            return $"Error setting disk spin-down: {ex.Message}";
        }
    }

    public async Task<string> SetDiskApmLevelAsync(string devicePath, int level)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return "Disk power management is only supported on Linux";
        }

        if (!ValidateDevicePath(devicePath))
        {
            return "Invalid device path";
        }

        if (level < 1 || level > 255)
        {
            return "APM level must be between 1 and 255 (1=minimum power, 255=maximum performance)";
        }

        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "hdparm",
                Arguments = $"-B {level} {devicePath}",
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
                    return $"Set APM level to {level} for {devicePath}";
                }
                else
                {
                    return $"Failed to set APM level: {error}";
                }
            }

            return "Failed to start hdparm process";
        }
        catch (Exception ex)
        {
            return $"Error setting APM level: {ex.Message}";
        }
    }

    public async Task<string> GetDiskPowerStatusAsync(string devicePath)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return "Disk power status check is only supported on Linux";
        }

        if (!ValidateDevicePath(devicePath))
        {
            return "Invalid device path";
        }

        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "hdparm",
                Arguments = $"-C {devicePath}",
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
                    return output;
                }
                else
                {
                    return $"Failed to get power status: {error}";
                }
            }

            return "Failed to start hdparm process";
        }
        catch (Exception ex)
        {
            return $"Error getting disk power status: {ex.Message}";
        }
    }

    private static bool ValidateDevicePath(string devicePath)
    {
        if (string.IsNullOrWhiteSpace(devicePath))
        {
            return false;
        }

        if (InvalidChars.Any(c => devicePath.Contains(c)))
        {
            return false;
        }

        if (!devicePath.StartsWith("/dev/", StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    private static bool ValidateMountPoint(string mountPoint)
    {
        if (string.IsNullOrWhiteSpace(mountPoint))
        {
            return false;
        }

        if (InvalidChars.Any(c => mountPoint.Contains(c)))
        {
            return false;
        }

        if (!Path.IsPathRooted(mountPoint))
        {
            return false;
        }

        return true;
    }
}

// JSON models for lsblk output
internal class LsblkOutput
{
    public List<LsblkDevice>? Blockdevices { get; set; }
}

internal class LsblkDevice
{
    public string? Name { get; set; }
    public string? Type { get; set; }
    public long Size { get; set; }
    public string? Mountpoint { get; set; }
    public string? Fstype { get; set; }
    public string? Uuid { get; set; }
    public string? Label { get; set; }
    public bool Rm { get; set; }
    public bool Ro { get; set; }
    public string? Model { get; set; }
    public string? Serial { get; set; }
    public List<LsblkDevice>? Children { get; set; }
}
