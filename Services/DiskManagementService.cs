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

            var lsblkData = JsonSerializer.Deserialize<LsblkOutput>(
                output,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
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

        if (fileSystem != null && InvalidChars.Any(c => fileSystem.Contains(c)))
        {
            return "Invalid characters in filesystem type";
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

            // Use File.AppendAllText for safer file writing instead of shell command
            try
            {
                var fstabPath = "/etc/fstab";
                string[] existingLines;

                try
                {
                    existingLines = await File.ReadAllLinesAsync(fstabPath);
                }
                catch (FileNotFoundException)
                {
                    existingLines = Array.Empty<string>();
                }

                foreach (var line in existingLines)
                {
                    if (line.Trim() == fstabEntry)
                    {
                        return $"Successfully mounted {devicePath} to {mountPoint}; matching entry already exists in /etc/fstab";
                    }
                }

                await File.AppendAllTextAsync(fstabPath, fstabEntry + Environment.NewLine);
                return $"Successfully mounted {devicePath} to {mountPoint} and added to /etc/fstab";
            }
            catch (UnauthorizedAccessException)
            {
                return $"Mounted successfully but permission denied writing to /etc/fstab. Entry: {fstabEntry}";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error appending to /etc/fstab for device '{devicePath}' at mount point '{mountPoint}'. Exception: {ex}");
                return $"Mounted successfully but error updating /etc/fstab: {ex.Message}. Entry: {fstabEntry}";
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error during permanent mount setup for device '{devicePath}' at mount point '{mountPoint}'. Exception: {ex}");
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
            // Convert minutes to hdparm units
            // hdparm uses 5-second units, so: minutes * 60 seconds / 5 = minutes * 12
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

                return process.ExitCode == 0
                    ? $"Set APM level to {level} for {devicePath}"
                    : $"Failed to set APM level: {error}";
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
    
    // Network disk management implementation
    public async Task<List<NetworkDiskInfo>> GetNetworkDisksAsync()
    {
        var networkDisks = new List<NetworkDiskInfo>();
        
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return networkDisks;
        }
        
        try
        {
            // Read /proc/mounts to find network mounts
            var mountsPath = "/proc/mounts";
            if (!File.Exists(mountsPath))
            {
                return networkDisks;
            }
            
            var lines = await File.ReadAllLinesAsync(mountsPath);
            foreach (var line in lines)
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 4)
                    continue;
                    
                var device = parts[0];
                var mountPoint = parts[1];
                var fsType = parts[2];
                var options = parts.Length > 3 ? parts[3] : "";
                
                // Check if it's a network filesystem
                if (fsType == "cifs" || fsType == "nfs" || fsType == "nfs4")
                {
                    var diskInfo = new NetworkDiskInfo
                    {
                        MountPoint = mountPoint,
                        FileSystem = fsType,
                        Options = options,
                        IsReady = true
                    };
                    
                    // Parse server and share path
                    if (fsType == "cifs")
                    {
                        diskInfo.DiskType = NetworkDiskType.CIFS;
                        // Format: //server/share
                        if (device.StartsWith("//") && device.Length > 2)
                        {
                            var pathParts = device[2..].Split('/', 2);
                            if (pathParts.Length >= 1)
                            {
                                diskInfo.Server = pathParts[0];
                                diskInfo.SharePath = pathParts.Length > 1 ? pathParts[1] : "";
                            }
                        }
                    }
                    else if (fsType == "nfs" || fsType == "nfs4")
                    {
                        diskInfo.DiskType = NetworkDiskType.NFS;
                        // Format: server:/path
                        var colonIndex = device.IndexOf(':');
                        if (colonIndex > 0 && colonIndex < device.Length - 1)
                        {
                            diskInfo.Server = device[..colonIndex];
                            diskInfo.SharePath = device[(colonIndex + 1)..];
                        }
                    }
                    
                    // Get usage information
                    if (Directory.Exists(mountPoint))
                    {
                        try
                        {
                            var driveInfo = new DriveInfo(mountPoint);
                            if (driveInfo.IsReady)
                            {
                                diskInfo.TotalBytes = driveInfo.TotalSize;
                                diskInfo.AvailableBytes = driveInfo.AvailableFreeSpace;
                                diskInfo.UsedBytes = driveInfo.TotalSize - driveInfo.AvailableFreeSpace;
                                diskInfo.UsagePercent = diskInfo.TotalBytes > 0
                                    ? Math.Round((double)diskInfo.UsedBytes / diskInfo.TotalBytes * 100, 2)
                                    : 0;
                            }
                        }
                        catch
                        {
                            // Ignore errors getting usage info
                        }
                    }
                    
                    networkDisks.Add(diskInfo);
                }
            }
        }
        catch
        {
            // Return empty list on error
        }
        
        return networkDisks;
    }
    
    public async Task<string> MountNetworkDiskAsync(string server, string sharePath, string mountPoint, NetworkDiskType diskType, string? username = null, string? password = null, string? domain = null, string? options = null)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return "Network disk mounting is only supported on Linux";
        }
        
        if (!ValidateNetworkPath(server, sharePath))
        {
            return "Invalid server or share path";
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
            
            string device;
            string mountCommand;
            string arguments;
            string? tempCredFile = null;
            
            if (diskType == NetworkDiskType.CIFS)
            {
                // CIFS mount
                device = $"//{server}/{sharePath}";
                
                // Build credentials using a temporary credentials file for security
                var credOptions = new List<string>();
                
                // If credentials are provided, create a temporary credential file
                if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
                {
                    try
                    {
                        // Create a temporary credential file
                        tempCredFile = Path.Combine("/tmp", $"cifs-cred-{Guid.NewGuid()}");
                        var credContent = $"username={username}\npassword={password}\n";
                        if (!string.IsNullOrEmpty(domain))
                        {
                            credContent += $"domain={domain}\n";
                        }
                        await File.WriteAllTextAsync(tempCredFile, credContent);
                        
                        // Set secure permissions on credential file
                        var chmodInfo = new ProcessStartInfo
                        {
                            FileName = "chmod",
                            Arguments = $"600 {tempCredFile}",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        using var chmodProcess = Process.Start(chmodInfo);
                        if (chmodProcess != null)
                        {
                            await chmodProcess.WaitForExitAsync();
                        }
                        
                        credOptions.Add($"credentials={tempCredFile}");
                    }
                    catch
                    {
                        // If we can't create credential file, skip credentials
                        // This is safer than putting passwords in command line
                        if (tempCredFile != null && File.Exists(tempCredFile))
                        {
                            try { File.Delete(tempCredFile); } catch { }
                            tempCredFile = null;
                        }
                    }
                }
                
                var allOptions = string.IsNullOrEmpty(options) 
                    ? string.Join(",", credOptions)
                    : string.Join(",", credOptions.Concat(options.Split(',')));
                
                arguments = $"-t cifs {device} {mountPoint}";
                if (!string.IsNullOrEmpty(allOptions))
                {
                    arguments = $"-o {allOptions} {arguments}";
                }
                
                mountCommand = "mount";
            }
            else // NFS
            {
                // NFS mount
                device = $"{server}:/{sharePath.TrimStart('/')}";
                arguments = $"{device} {mountPoint}";
                
                if (!string.IsNullOrEmpty(options))
                {
                    arguments = $"-o {options} {arguments}";
                }
                
                mountCommand = "mount";
            }
            
            var processInfo = new ProcessStartInfo
            {
                FileName = mountCommand,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            try
            {
                using var process = Process.Start(processInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    var output = await process.StandardOutput.ReadToEndAsync();
                    var error = await process.StandardError.ReadToEndAsync();
                    
                    if (process.ExitCode == 0)
                    {
                        return $"Successfully mounted {device} to {mountPoint}";
                    }
                    else
                    {
                        return $"Failed to mount: {error}";
                    }
                }
                
                return "Failed to start mount process";
            }
            finally
            {
                // Clean up temporary credential file
                if (tempCredFile != null && File.Exists(tempCredFile))
                {
                    try { File.Delete(tempCredFile); } catch { }
                }
            }
        }
        catch (Exception ex)
        {
            return $"Error mounting network disk: {ex.Message}";
        }
    }
    
    public async Task<string> MountNetworkDiskPermanentAsync(string server, string sharePath, string mountPoint, NetworkDiskType diskType, string? username = null, string? password = null, string? domain = null, string? options = null)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return "Permanent network disk mounting is only supported on Linux";
        }
        
        // First mount temporarily
        var mountResult = await MountNetworkDiskAsync(server, sharePath, mountPoint, diskType, username, password, domain, options);
        if (!mountResult.Contains("Successfully"))
        {
            return mountResult;
        }
        
        try
        {
            string device;
            string fsType;
            var fstabOptions = new List<string>();
            
            if (diskType == NetworkDiskType.CIFS)
            {
                device = $"//{server}/{sharePath}";
                fsType = "cifs";
                
                // For CIFS, we should use a credentials file for security
                if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
                {
                    // Use a hash-based naming scheme to avoid special character issues
                    var hashInput = $"{server}-{sharePath}";
                    var hashBytes = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(hashInput));
                    var hashString = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                    var credFile = $"/etc/cifs-credentials-{hashString}";
                    try
                    {
                        var credContent = $"username={username}\npassword={password}\n";
                        if (!string.IsNullOrEmpty(domain))
                        {
                            credContent += $"domain={domain}\n";
                        }
                        await File.WriteAllTextAsync(credFile, credContent);
                        
                        // Set secure permissions on credential file
                        var chmodInfo = new ProcessStartInfo
                        {
                            FileName = "chmod",
                            Arguments = $"600 {credFile}",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        using var chmodProcess = Process.Start(chmodInfo);
                        if (chmodProcess != null)
                        {
                            await chmodProcess.WaitForExitAsync();
                        }
                        
                        fstabOptions.Add($"credentials={credFile}");
                    }
                    catch
                    {
                        // If we can't write credentials file, fall back to username in fstab
                        fstabOptions.Add($"username={username}");
                        if (!string.IsNullOrEmpty(domain))
                        {
                            fstabOptions.Add($"domain={domain}");
                        }
                    }
                }
            }
            else // NFS
            {
                device = $"{server}:/{sharePath.TrimStart('/')}";
                fsType = "nfs";
            }
            
            // Add custom options
            if (!string.IsNullOrEmpty(options))
            {
                fstabOptions.AddRange(options.Split(','));
            }
            
            // Add default options if none specified
            if (fstabOptions.Count == 0)
            {
                fstabOptions.Add("defaults");
            }
            
            var fstabEntry = $"{device} {mountPoint} {fsType} {string.Join(",", fstabOptions)} 0 0";
            
            try
            {
                var fstabPath = "/etc/fstab";
                string[] existingLines;
                
                try
                {
                    existingLines = await File.ReadAllLinesAsync(fstabPath);
                }
                catch (FileNotFoundException)
                {
                    existingLines = Array.Empty<string>();
                }
                
                // Check if entry already exists
                foreach (var line in existingLines)
                {
                    if (line.Trim().StartsWith(device) && line.Contains(mountPoint))
                    {
                        return $"Successfully mounted {device} to {mountPoint}; matching entry already exists in /etc/fstab";
                    }
                }
                
                await File.AppendAllTextAsync(fstabPath, fstabEntry + Environment.NewLine);
                return $"Successfully mounted {device} to {mountPoint} and added to /etc/fstab";
            }
            catch (UnauthorizedAccessException)
            {
                return $"Mounted successfully but permission denied writing to /etc/fstab. Entry: {fstabEntry}";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error appending to /etc/fstab for network disk '{device}' at mount point '{mountPoint}'. Exception: {ex}");
                return $"Mounted successfully but error updating /etc/fstab: {ex.Message}. Entry: {fstabEntry}";
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error during permanent network mount setup. Exception: {ex}");
            return $"Mounted successfully but error updating /etc/fstab: {ex.Message}";
        }
    }
    
    private static bool ValidateNetworkPath(string server, string sharePath)
    {
        if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(sharePath))
        {
            return false;
        }
        
        if (InvalidChars.Any(c => server.Contains(c) || sharePath.Contains(c)))
        {
            return false;
        }
        
        return true;
    }
}

// JSON models for lsblk output
/// <summary>
/// Represents the root object of the JSON output produced by the <c>lsblk</c> command.
/// </summary>
internal class LsblkOutput
{
    /// <summary>
    /// Gets or sets the collection of top-level block devices returned by <c>lsblk</c> in the
    /// <c>"blockdevices"</c> array.
    /// </summary>
    public List<LsblkDevice>? Blockdevices { get; set; }
}

/// <summary>
/// Represents a single block device entry from the JSON output of the <c>lsblk</c> command.
/// </summary>
internal class LsblkDevice
{
    /// <summary>
    /// Gets or sets the device name (for example, <c>sda</c> or <c>sda1</c>), corresponding to the
    /// <c>"name"</c> field in <c>lsblk</c> JSON output.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the device type (for example, <c>disk</c>, <c>part</c>, or <c>rom</c>),
    /// corresponding to the <c>"type"</c> field.
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Gets or sets the size of the device in bytes, corresponding to the <c>"size"</c> field.
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Gets or sets the mount point of the device, if any, corresponding to the
    /// <c>"mountpoint"</c> field.
    /// </summary>
    public string? Mountpoint { get; set; }

    /// <summary>
    /// Gets or sets the filesystem type on the device, such as <c>ext4</c> or <c>ntfs</c>,
    /// corresponding to the <c>"fstype"</c> field.
    /// </summary>
    public string? Fstype { get; set; }

    /// <summary>
    /// Gets or sets the filesystem UUID associated with the device, corresponding to the
    /// <c>"uuid"</c> field.
    /// </summary>
    public string? Uuid { get; set; }

    /// <summary>
    /// Gets or sets the filesystem label assigned to the device, corresponding to the
    /// <c>"label"</c> field.
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the device is removable,
    /// corresponding to the <c>"rm"</c> field.
    /// </summary>
    public bool Rm { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the device is read-only,
    /// corresponding to the <c>"ro"</c> field.
    /// </summary>
    public bool Ro { get; set; }

    /// <summary>
    /// Gets or sets the device model string reported by the system, corresponding to the
    /// <c>"model"</c> field.
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Gets or sets the device serial number, if available, corresponding to the
    /// <c>"serial"</c> field.
    /// </summary>
    public string? Serial { get; set; }

    /// <summary>
    /// Gets or sets the collection of child devices for this block device, such as partitions
    /// or logical volumes, corresponding to the nested <c>"children"</c> array.
    /// </summary>
    public List<LsblkDevice>? Children { get; set; }
}
