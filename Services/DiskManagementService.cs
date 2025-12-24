using QingFeng.Models;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace QingFeng.Services;

public class DiskManagementService : IDiskManagementService
{
    private static readonly string[] InvalidChars = ["&&", ";", "|", "`", "$", "(", ")", "<", ">", "\"", "'", "\n", "\r", " "];
    private static readonly char[] InvalidCredentialChars = ['\n', '\r', '='];
    private static readonly HashSet<char> InvalidCredentialCharsSet = new(InvalidCredentialChars);
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);
    
    // Known valid hdparm flags (without assignment)
    private static readonly HashSet<string> ValidHdparmFlags = new(StringComparer.OrdinalIgnoreCase)
    {
        "quiet", "standby", "sleep", "disable_seagate"
    };
    
    // Known valid hdparm parameters (with assignment)
    private static readonly HashSet<string> ValidHdparmParams = new(StringComparer.OrdinalIgnoreCase)
    {
        "read_ahead_sect", "lookahead", "bus", "apm", "apm_battery", "io32_support",
        "dma", "defect_mgmt", "cd_speed", "keep_settings_over_reset", 
        "keep_features_over_reset", "mult_sect_io", "prefetch_sect", "read_only",
        "write_read_verify", "poweron_standby", "spindown_time", "force_spindown_time",
        "interrupt_unmask", "write_cache", "transfer_mode", "acoustic_management",
        "chipset_pio_mode", "security_freeze", "security_unlock", "security_pass",
        "security_disable", "user-master", "security_mode"
    };
    
    private static readonly Regex HdparmSettingRegex = new(@"^\s*([a-z_-]+)\s*=\s*(.+)\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

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

        if (timeoutMinutes < 0 || timeoutMinutes > 330)
        {
            return "Timeout must be between 0 and 330 minutes (0 = disabled, max 5.5 hours)";
        }

        try
        {
            // Update /etc/hdparm.conf for persistent settings
            var confResult = await UpdateHdparmConfAsync(devicePath, spindownTime: timeoutMinutes);
            if (!confResult.Success)
            {
                return confResult.Message;
            }

            // Convert minutes to hdparm encoding (0-255)
            var hdparmValue = ConvertMinutesToHdparmEncoding(timeoutMinutes).ToString();

            // Also apply immediately using hdparm command
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
                        ? $"Successfully disabled spin-down for {devicePath} (persistent)"
                        : $"Successfully set spin-down timeout to {timeoutMinutes} minutes for {devicePath} (persistent)";
                    return msg;
                }
                else
                {
                    return $"Configuration saved, but failed to apply immediately: {error}";
                }
            }

            return "Configuration saved, but failed to start hdparm process";
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
            // Update /etc/hdparm.conf for persistent settings
            var confResult = await UpdateHdparmConfAsync(devicePath, apmLevel: level);
            if (!confResult.Success)
            {
                return confResult.Message;
            }

            // Also apply immediately using hdparm command
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
                    ? $"Successfully set APM level to {level} for {devicePath} (persistent)"
                    : $"Configuration saved, but failed to apply immediately: {error}";
            }

            return "Configuration saved, but failed to start hdparm process";
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

    /// <summary>
    /// Converts minutes to hdparm standby timeout encoding.
    /// hdparm -S uses a special encoding:
    /// - 0 = disabled
    /// - 1-240 = multiples of 5 seconds (5 seconds to 20 minutes)
    /// - 241-251 = multiples of 30 minutes (30 minutes to 5.5 hours)
    /// - 252 = 21 minutes
    /// - 253 = vendor-defined (8-12 hours)
    /// - 254 = reserved
    /// - 255 = 21 minutes + 15 seconds
    /// </summary>
    /// <param name="minutes">Timeout in minutes (0-330)</param>
    /// <returns>hdparm encoded value (0-255)</returns>
    private static int ConvertMinutesToHdparmEncoding(int minutes)
    {
        if (minutes <= 0)
        {
            return 0; // Disabled
        }
        
        if (minutes <= 20)
        {
            // 1-240: multiples of 5 seconds
            // minutes * 60 seconds / 5 = minutes * 12
            return minutes * 12;
        }
        
        if (minutes == 21)
        {
            return 252; // Special value for exactly 21 minutes
        }
        
        if (minutes <= 330)
        {
            // 241-251: multiples of 30 minutes (30 to 330 minutes = 5.5 hours)
            // 241 = 30 min, 242 = 60 min, ..., 251 = 330 min
            int thirtyMinuteUnits = (minutes + 29) / 30; // Round up to nearest 30 min unit
            return 240 + thirtyMinuteUnits;
        }
        
        // For values > 330 minutes, use the highest available value (251 = 330 minutes)
        return 251;
    }
    
    /// <summary>
    /// Result of hdparm configuration update operation
    /// </summary>
    private record HdparmConfigResult(bool Success, string Message);

    /// <summary>
    /// Updates /etc/hdparm.conf with persistent power management settings for a device.
    /// Creates or updates the device block in the configuration file.
    /// </summary>
    /// <param name="devicePath">The device path (e.g., /dev/sda)</param>
    /// <param name="spindownTime">Optional spindown timeout in minutes (0-330). Uses hdparm's encoding scheme internally.</param>
    /// <param name="apmLevel">Optional APM level (1-255)</param>
    /// <returns>Result indicating success or failure with a message</returns>
    private static async Task<HdparmConfigResult> UpdateHdparmConfAsync(string devicePath, int? spindownTime = null, int? apmLevel = null)
    {
        const string hdparmConfPath = "/etc/hdparm.conf";

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return new HdparmConfigResult(false, "Updating /etc/hdparm.conf is only supported on Linux systems.");
        }

        // Fail fast if the application is not running with sufficient privileges (typically root).
        // Note: This is a heuristic check - actual write permissions may still vary
        if (!string.Equals(Environment.UserName, "root", StringComparison.Ordinal))
        {
            return new HdparmConfigResult(false, "Failed to update /etc/hdparm.conf: Permission denied. The application needs to run with sufficient privileges (e.g., as root).");
        }

        try
        {
            // Read existing configuration or create new one
            var lines = new List<string>();
            if (File.Exists(hdparmConfPath))
            {
                lines = (await File.ReadAllLinesAsync(hdparmConfPath)).ToList();
            }
            else
            {
                // Create a basic hdparm.conf header if file doesn't exist
                lines.Add("## hdparm configuration file");
                lines.Add("## Auto-generated by QingFeng disk management");
                lines.Add("");
                lines.Add("quiet");
                lines.Add("");
            }

            // Find if a block for this device already exists
            int blockStartIndex = -1;
            int blockEndIndex = -1;
            
            for (int i = 0; i < lines.Count; i++)
            {
                var trimmedLine = lines[i].Trim();
                // Use exact matching on the device header line to avoid partial device path matches
                // e.g., when searching for /dev/sda, do not accidentally identify the /dev/sda1 block
                if (trimmedLine == $"{devicePath} {{")
                {
                    blockStartIndex = i;
                    // Find the closing brace
                    for (int j = i + 1; j < lines.Count; j++)
                    {
                        if (lines[j].Trim() == "}")
                        {
                            blockEndIndex = j;
                            break;
                        }
                    }
                    break;
                }
            }

            // Build the device block content
            var blockLines = new List<string>();
            var existingSpindown = false;
            var existingApm = false;

            if (blockStartIndex >= 0 && blockEndIndex >= 0)
            {
                // Parse existing block to preserve other settings
                for (int i = blockStartIndex + 1; i < blockEndIndex; i++)
                {
                    var line = lines[i].Trim();
                    
                    // Skip comments and empty lines
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    {
                        continue;
                    }
                    
                    // Extract parameter name for exact matching
                    // Split on first '=' only to handle values that might contain '='
                    var parts = line.Split('=', 2);
                    var paramName = parts[0].Trim();
                    
                    if (paramName.Equals("spindown_time", StringComparison.OrdinalIgnoreCase) ||
                        paramName.Equals("force_spindown_time", StringComparison.OrdinalIgnoreCase))
                    {
                        existingSpindown = true;
                        if (spindownTime.HasValue)
                        {
                            var hdparmValue = ConvertMinutesToHdparmEncoding(spindownTime.Value);
                            blockLines.Add($"\tspindown_time = {hdparmValue}");
                        }
                        else
                        {
                            // Preserve existing spindown_time if not being updated
                            blockLines.Add($"\t{line}");
                        }
                    }
                    else if (paramName.Equals("apm", StringComparison.OrdinalIgnoreCase))
                    {
                        existingApm = true;
                        if (apmLevel.HasValue)
                        {
                            blockLines.Add($"\tapm = {apmLevel.Value}");
                        }
                        else
                        {
                            // Preserve existing apm if not being updated
                            blockLines.Add($"\t{line}");
                        }
                    }
                    else
                    {
                        // Preserve other non-comment settings that are valid
                        // Check if it's a valid flag or a valid parameter with assignment
                        var match = HdparmSettingRegex.Match(line);
                        if (match.Success && ValidHdparmParams.Contains(match.Groups[1].Value))
                        {
                            blockLines.Add($"\t{line}");
                        }
                        else if (ValidHdparmFlags.Contains(paramName))
                        {
                            blockLines.Add($"\t{line}");
                        }
                    }
                }
            }

            // Add new settings if they weren't in the existing block
            if (spindownTime.HasValue && !existingSpindown)
            {
                var hdparmValue = ConvertMinutesToHdparmEncoding(spindownTime.Value);
                blockLines.Add($"\tspindown_time = {hdparmValue}");
            }
            if (apmLevel.HasValue && !existingApm)
            {
                blockLines.Add($"\tapm = {apmLevel.Value}");
            }

            // Build the complete new block
            var newBlock = new List<string>
            {
                $"{devicePath} {{",
            };
            newBlock.AddRange(blockLines);
            newBlock.Add("}");

            // Remove old block if it exists
            if (blockStartIndex >= 0 && blockEndIndex >= 0)
            {
                lines.RemoveRange(blockStartIndex, blockEndIndex - blockStartIndex + 1);
            }

            // Add the new block at the end
            if (lines.Count > 0 && !string.IsNullOrWhiteSpace(lines[^1]))
            {
                lines.Add(""); // Add blank line before new block
            }
            lines.AddRange(newBlock);

            // Write the updated configuration using atomic file operations
            // Write to a temporary file first, then move it to the final location
            // This prevents corruption if the write operation fails
            var tempPath = $"{hdparmConfPath}.tmp";
            try
            {
                await File.WriteAllLinesAsync(tempPath, lines);
                
                // Move the temporary file to the final location
                // This is an atomic operation on most filesystems
                File.Move(tempPath, hdparmConfPath, overwrite: true);
            }
            catch
            {
                // Clean up temporary file if it exists
                if (File.Exists(tempPath))
                {
                    try 
                    { 
                        File.Delete(tempPath); 
                    } 
                    catch (IOException ex)
                    { 
                        // Ignore cleanup errors - temporary file will be cleaned up by system eventually
                        Debug.WriteLine($"Failed to delete temporary file '{tempPath}' during cleanup: {ex}");
                    }
                }
                throw;
            }

            return new HdparmConfigResult(true, "Successfully updated /etc/hdparm.conf");
        }
        catch (UnauthorizedAccessException)
        {
            return new HdparmConfigResult(false, "Failed to update /etc/hdparm.conf: Permission denied. The application needs to run with sufficient privileges.");
        }
        catch (Exception ex)
        {
            return new HdparmConfigResult(false, $"Failed to update /etc/hdparm.conf: {ex.Message}");
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
        
        // Validate credentials don't contain invalid characters
        if (!string.IsNullOrEmpty(username) && username.Any(c => InvalidCredentialCharsSet.Contains(c)))
        {
            return "Username contains invalid characters (newline or equals sign)";
        }
        if (!string.IsNullOrEmpty(password) && password.Any(c => InvalidCredentialCharsSet.Contains(c)))
        {
            return "Password contains invalid characters (newline or equals sign)";
        }
        if (!string.IsNullOrEmpty(domain) && domain.Any(c => InvalidCredentialCharsSet.Contains(c)))
        {
            return "Domain contains invalid characters (newline or equals sign)";
        }
        
        try
        {
            // Create mount point if it doesn't exist
            if (!Directory.Exists(mountPoint))
            {
                Directory.CreateDirectory(mountPoint);
            }
            
            string device;
            string? tempCredFile = null;
            
            var processInfo = new ProcessStartInfo
            {
                FileName = "mount",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            if (diskType == NetworkDiskType.CIFS)
            {
                // CIFS mount
                device = $"//{server}/{sharePath}";
                
                processInfo.ArgumentList.Add("-t");
                processInfo.ArgumentList.Add("cifs");
                
                // Build credentials using a temporary credentials file for security
                var credOptions = new List<string>();
                
                // If credentials are provided, create a temporary credential file with secure permissions
                if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
                {
                    try
                    {
                        // Use SHA256 hash for better collision resistance
                        var hashInput = $"{Guid.NewGuid()}-{DateTime.UtcNow.Ticks}";
                        var hashBytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(hashInput));
                        var hashString = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant()[..16];
                        tempCredFile = Path.Combine("/tmp", $"cifs-cred-{hashString}");
                        
                        var credContent = $"username={username}\npassword={password}\n";
                        if (!string.IsNullOrEmpty(domain))
                        {
                            credContent += $"domain={domain}\n";
                        }
                        
                        // Create the credential file with secure permissions atomically
                        var fsOptions = new FileStreamOptions
                        {
                            Mode = FileMode.CreateNew,
                            Access = FileAccess.Write,
                            Share = FileShare.None,
                            Options = FileOptions.Asynchronous,
                            UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite
                        };

                        await using (var fs = new FileStream(tempCredFile, fsOptions))
                        {
                            await using (var writer = new StreamWriter(fs))
                            {
                                await writer.WriteAsync(credContent);
                            }
                        }
                        
                        credOptions.Add($"credentials={tempCredFile}");
                    }
                    catch (Exception ex)
                    {
                        // Clean up the credential file if it was created
                        if (tempCredFile != null && File.Exists(tempCredFile))
                        {
                            try { File.Delete(tempCredFile); } catch { }
                            tempCredFile = null;
                        }
                        Debug.WriteLine($"Failed to create secure credential file: {ex}");
                        return $"Failed to create secure credential file: {ex.Message}";
                    }
                }
                
                // Add custom options
                if (!string.IsNullOrEmpty(options))
                {
                    credOptions.AddRange(options.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(o => o.Trim()));
                }
                
                if (credOptions.Count > 0)
                {
                    processInfo.ArgumentList.Add("-o");
                    processInfo.ArgumentList.Add(string.Join(",", credOptions));
                }
                
                processInfo.ArgumentList.Add(device);
                processInfo.ArgumentList.Add(mountPoint);
            }
            else // NFS
            {
                // NFS mount - normalize share path
                var normalizedSharePath = sharePath.StartsWith("/") ? sharePath : "/" + sharePath;
                device = $"{server}:{normalizedSharePath}";
                
                if (!string.IsNullOrEmpty(options))
                {
                    processInfo.ArgumentList.Add("-o");
                    processInfo.ArgumentList.Add(options);
                }
                
                processInfo.ArgumentList.Add(device);
                processInfo.ArgumentList.Add(mountPoint);
            }
            
            try
            {
                using var process = Process.Start(processInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    var error = await process.StandardError.ReadToEndAsync();
                    
                    return process.ExitCode == 0
                        ? $"Successfully mounted {device} to {mountPoint}"
                        : $"Failed to mount: {error}";
                }
                
                return "Failed to start mount process";
            }
            finally
            {
                // Clean up temporary credential file
                if (tempCredFile != null && File.Exists(tempCredFile))
                {
                    try
                    {
                        File.Delete(tempCredFile);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to delete temporary credential file '{tempCredFile}': {ex}");
                    }
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
                    // Use SHA256 hash for better collision resistance
                    var hashInput = $"{server}-{sharePath}";
                    var hashBytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(hashInput));
                    var hashString = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant()[..32];
                    var credFile = $"/etc/cifs-credentials-{hashString}";
                    try
                    {
                        var credContent = $"username={username}\npassword={password}\n";
                        if (!string.IsNullOrEmpty(domain))
                        {
                            credContent += $"domain={domain}\n";
                        }
                        
                        // Create the credential file with secure permissions atomically
                        var fsOptions = new FileStreamOptions
                        {
                            Mode = FileMode.Create,
                            Access = FileAccess.Write,
                            Share = FileShare.None,
                            Options = FileOptions.Asynchronous,
                            UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite
                        };

                        await using (var fs = new FileStream(credFile, fsOptions))
                        {
                            await using (var writer = new StreamWriter(fs))
                            {
                                await writer.WriteAsync(credContent);
                            }
                        }
                        
                        // Verify file permissions were set correctly
                        var chmodInfo = new ProcessStartInfo
                        {
                            FileName = "chmod",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        chmodInfo.ArgumentList.Add("600");
                        chmodInfo.ArgumentList.Add(credFile);
                        
                        using var chmodProcess = Process.Start(chmodInfo);
                        if (chmodProcess == null)
                        {
                            throw new InvalidOperationException("Failed to start chmod process to secure credential file.");
                        }

                        await chmodProcess.WaitForExitAsync();
                        if (chmodProcess.ExitCode != 0)
                        {
                            var errorOutput = await chmodProcess.StandardError.ReadToEndAsync();
                            // Remove potentially insecure credential file
                            try
                            {
                                if (File.Exists(credFile))
                                {
                                    File.Delete(credFile);
                                }
                            }
                            catch
                            {
                                // Ignore file deletion errors
                            }
                            throw new InvalidOperationException($"chmod failed for credential file '{credFile}' with exit code {chmodProcess.ExitCode}: {errorOutput}");
                        }
                        
                        fstabOptions.Add($"credentials={credFile}");
                    }
                    catch (Exception ex)
                    {
                        // If we can't securely write credentials file, fail the operation
                        try
                        {
                            if (File.Exists(credFile))
                            {
                                File.Delete(credFile);
                            }
                        }
                        catch
                        {
                            // Ignore file deletion errors
                        }
                        Debug.WriteLine($"Failed to create CIFS credentials file: {ex}");
                        return $"Failed to create secure CIFS credentials file '{credFile}'. Refusing to store credentials insecurely in /etc/fstab. Error: {ex.Message}";
                    }
                }
            }
            else // NFS
            {
                // NFS mount - normalize share path
                var normalizedSharePath = sharePath.Trim().TrimStart('/');
                if (string.IsNullOrEmpty(normalizedSharePath))
                {
                    return "Invalid NFS share path: share path must not be empty or consist only of '/'.";
                }
                
                device = $"{server}:/{normalizedSharePath}";
                fsType = "nfs";
            }
            
            // Add custom options with validation
            if (!string.IsNullOrEmpty(options))
            {
                foreach (var rawOption in options.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    var option = rawOption.Trim();
                    if (string.IsNullOrEmpty(option))
                    {
                        continue;
                    }
                    
                    // Reject options containing invalid characters or whitespace
                    if (InvalidChars.Any(c => option.Contains(c)) || option.Any(char.IsWhiteSpace))
                    {
                        Debug.WriteLine($"Ignoring invalid fstab option: '{rawOption}' after trimming to '{option}'.");
                        continue;
                    }
                    
                    fstabOptions.Add(option);
                }
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
                
                // Check if entry already exists using proper field parsing
                var hasExistingEntry = existingLines
                    .Select(line => line.Trim())
                    .Where(trimmed => !string.IsNullOrEmpty(trimmed) && !trimmed.StartsWith("#"))
                    .Select(trimmed => WhitespaceRegex.Split(trimmed))
                    .Where(fields => fields.Length >= 2)
                    .Any(fields => fields[0] == device && fields[1] == mountPoint);

                if (hasExistingEntry)
                {
                    return $"Successfully mounted {device} to {mountPoint}; matching entry already exists in /etc/fstab";
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
