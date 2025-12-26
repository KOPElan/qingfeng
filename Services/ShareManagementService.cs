using QingFeng.Models;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace QingFeng.Services;

public class ShareManagementService : IShareManagementService
{
    private const string SambaConfigPath = "/etc/samba/smb.conf";
    private const string NfsExportsPath = "/etc/exports";
    
    private static readonly string[] InvalidChars = ["&&", ";", "|", "`", "$", "<", ">", "\"", "'", "\n", "\r", "\t"];
    private static readonly Regex ShareNameRegex = new(@"^\[([^\]]+)\]$", RegexOptions.Compiled);
    private static readonly Regex ParameterRegex = new(@"^\s*([a-zA-Z0-9_\-\s]+?)\s*=\s*(.+?)\s*$", RegexOptions.Compiled);
    private static readonly Regex HostOptionRegex = new(@"([^\s(]+)\(([^)]+)\)", RegexOptions.Compiled);
    private static readonly Regex SafeNameRegex = new(@"^[a-zA-Z0-9_.\-]+$", RegexOptions.Compiled);
    
    public async Task<List<ShareInfo>> GetAllSharesAsync()
    {
        var shares = new List<ShareInfo>();
        shares.AddRange(await GetCifsSharesAsync());
        shares.AddRange(await GetNfsSharesAsync());
        return shares;
    }
    
    public async Task<List<ShareInfo>> GetCifsSharesAsync()
    {
        var shares = new List<ShareInfo>();
        
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return shares;
        }
        
        try
        {
            if (!File.Exists(SambaConfigPath))
            {
                return shares;
            }
            
            var lines = await File.ReadAllLinesAsync(SambaConfigPath);
            ShareInfo? currentShare = null;
            
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                
                // Skip comments and empty lines
                if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith("#") || trimmedLine.StartsWith(";"))
                {
                    continue;
                }
                
                // Check for share section header
                var shareMatch = ShareNameRegex.Match(trimmedLine);
                if (shareMatch.Success)
                {
                    // Save previous share if it exists
                    if (currentShare != null && !string.IsNullOrEmpty(currentShare.Path))
                    {
                        shares.Add(currentShare);
                    }
                    
                    var shareName = shareMatch.Groups[1].Value;
                    
                    // Skip global, homes, printers sections
                    if (shareName.Equals("global", StringComparison.OrdinalIgnoreCase) ||
                        shareName.Equals("homes", StringComparison.OrdinalIgnoreCase) ||
                        shareName.Equals("printers", StringComparison.OrdinalIgnoreCase))
                    {
                        currentShare = null;
                        continue;
                    }
                    
                    currentShare = new ShareInfo
                    {
                        Name = shareName,
                        Type = ShareType.CIFS
                    };
                    continue;
                }
                
                // Parse parameters for current share
                if (currentShare != null)
                {
                    var paramMatch = ParameterRegex.Match(trimmedLine);
                    if (paramMatch.Success)
                    {
                        var key = paramMatch.Groups[1].Value.Trim().ToLower();
                        var value = paramMatch.Groups[2].Value.Trim();
                        
                        switch (key)
                        {
                            case "path":
                                currentShare.Path = value;
                                break;
                            case "comment":
                                currentShare.Comment = value;
                                break;
                            case "browseable":
                            case "browsable":
                                currentShare.Browseable = ParseYesNo(value);
                                break;
                            case "read only":
                            case "readonly":
                                currentShare.ReadOnly = ParseYesNo(value);
                                break;
                            case "guest ok":
                            case "public":
                                currentShare.GuestOk = ParseYesNo(value);
                                break;
                            case "valid users":
                                currentShare.ValidUsers = value;
                                break;
                            case "write list":
                                currentShare.WriteList = value;
                                break;
                            default:
                                // Store other options
                                currentShare.CustomOptions[key] = value;
                                break;
                        }
                    }
                }
            }
            
            // Add the last share
            if (currentShare != null && !string.IsNullOrEmpty(currentShare.Path))
            {
                shares.Add(currentShare);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error reading CIFS shares: {ex.Message}");
        }
        
        return shares;
    }
    
    public async Task<List<ShareInfo>> GetNfsSharesAsync()
    {
        var shares = new List<ShareInfo>();
        
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return shares;
        }
        
        try
        {
            if (!File.Exists(NfsExportsPath))
            {
                return shares;
            }
            
            var lines = await File.ReadAllLinesAsync(NfsExportsPath);
            
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                
                // Skip comments and empty lines
                if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith("#"))
                {
                    continue;
                }
                
                // Parse NFS export line: /path/to/export host1(options) host2(options)
                // or simple: /path/to/export *(options)
                var parts = trimmedLine.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                {
                    continue;
                }
                
                var exportPath = parts[0];
                var hostsAndOptions = string.Join(" ", parts.Skip(1));
                
                // Extract allowed hosts and options
                var allowedHosts = new List<string>();
                var options = new List<string>();
                
                // Match patterns like: *(rw,sync) or 192.168.1.0/24(ro) or host.domain.com(rw)
                var matches = HostOptionRegex.Matches(hostsAndOptions);
                
                foreach (Match match in matches)
                {
                    if (match.Groups.Count >= 3)
                    {
                        allowedHosts.Add(match.Groups[1].Value);
                        options.Add(match.Groups[2].Value);
                    }
                }
                
                // Skip malformed lines that don't have valid host/option pairs
                if (allowedHosts.Count == 0 || options.Count == 0)
                {
                    continue;
                }
                
                // Determine read-only status: if ANY host has rw, share is not read-only
                var hasRw = options.Any(o => o.Contains("rw", StringComparison.OrdinalIgnoreCase));
                var hasRo = options.Any(o => o.Contains("ro", StringComparison.OrdinalIgnoreCase));
                var isReadOnly = !hasRw && hasRo;
                
                // Join distinct options for display
                var distinctOptions = options.Distinct().ToList();
                var nfsOptions = distinctOptions.Count == 1 
                    ? distinctOptions[0] 
                    : string.Join(" | ", distinctOptions);
                
                var share = new ShareInfo
                {
                    Name = Path.GetFileName(exportPath) ?? exportPath,
                    Path = exportPath,
                    Type = ShareType.NFS,
                    AllowedHosts = string.Join(", ", allowedHosts),
                    NfsOptions = nfsOptions,
                    ReadOnly = isReadOnly
                };
                
                shares.Add(share);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error reading NFS exports: {ex.Message}");
        }
        
        return shares;
    }
    
    public async Task<ShareInfo?> GetShareAsync(string name, ShareType type)
    {
        var shares = type == ShareType.CIFS 
            ? await GetCifsSharesAsync() 
            : await GetNfsSharesAsync();
            
        return shares.FirstOrDefault(s => 
            s.Name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
            (type == ShareType.NFS && s.Path.Equals(name, StringComparison.OrdinalIgnoreCase)));
    }
    
    public async Task<string> AddCifsShareAsync(ShareRequest request)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return "CIFS share management is only supported on Linux";
        }
        
        // Validate input
        var validation = ValidateShareRequest(request, ShareType.CIFS);
        if (!validation.isValid)
        {
            return validation.message;
        }
        
        try
        {
            // Check if share already exists
            var existingShares = await GetCifsSharesAsync();
            if (existingShares.Any(s => s.Name.Equals(request.Name, StringComparison.OrdinalIgnoreCase)))
            {
                return $"A share with name '{request.Name}' already exists";
            }
            
            // Verify the path exists
            if (!Directory.Exists(request.Path))
            {
                return $"Directory '{request.Path}' does not exist";
            }
            
            // Read existing configuration
            var lines = new List<string>();
            if (File.Exists(SambaConfigPath))
            {
                lines = (await File.ReadAllLinesAsync(SambaConfigPath)).ToList();
            }
            
            // Build share configuration
            var shareConfigLines = new List<string>
            {
                "",  // Empty line before share section
                $"[{request.Name}]",
                $"   path = {request.Path}"
            };
            
            if (!string.IsNullOrWhiteSpace(request.Comment))
            {
                shareConfigLines.Add($"   comment = {request.Comment}");
            }
            
            shareConfigLines.Add($"   browseable = {(request.Browseable ? "yes" : "no")}");
            shareConfigLines.Add($"   read only = {(request.ReadOnly ? "yes" : "no")}");
            shareConfigLines.Add($"   guest ok = {(request.GuestOk ? "yes" : "no")}");
            
            if (!string.IsNullOrWhiteSpace(request.ValidUsers))
            {
                shareConfigLines.Add($"   valid users = {request.ValidUsers}");
            }
            
            if (!string.IsNullOrWhiteSpace(request.WriteList) && request.ReadOnly)
            {
                shareConfigLines.Add($"   write list = {request.WriteList}");
            }
            
            // Append to configuration file
            lines.AddRange(shareConfigLines);
            
            // Write configuration atomically
            await WriteConfigFileAsync(SambaConfigPath, lines);
            
            // Test configuration
            var testResult = await TestSambaConfigAsync();
            if (testResult.Contains("failed", StringComparison.OrdinalIgnoreCase) || 
                testResult.Contains("error", StringComparison.OrdinalIgnoreCase))
            {
                return $"Configuration syntax error: {testResult}. Please check the share settings.";
            }
            
            return $"Successfully added CIFS share '{request.Name}'. Please restart Samba service to apply changes.";
        }
        catch (UnauthorizedAccessException)
        {
            return "Permission denied. The application needs root privileges to modify Samba configuration.";
        }
        catch (Exception ex)
        {
            return $"Error adding CIFS share: {ex.Message}";
        }
    }
    
    public async Task<string> AddNfsShareAsync(ShareRequest request)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return "NFS export management is only supported on Linux";
        }
        
        // Validate input
        var validation = ValidateShareRequest(request, ShareType.NFS);
        if (!validation.isValid)
        {
            return validation.message;
        }
        
        try
        {
            // Check if export already exists
            var existingExports = await GetNfsSharesAsync();
            if (existingExports.Any(s => s.Path.Equals(request.Path, StringComparison.OrdinalIgnoreCase)))
            {
                return $"An export for path '{request.Path}' already exists";
            }
            
            // Verify the path exists
            if (!Directory.Exists(request.Path))
            {
                return $"Directory '{request.Path}' does not exist";
            }
            
            // Build export line
            var allowedHosts = string.IsNullOrWhiteSpace(request.AllowedHosts) ? "*" : request.AllowedHosts;
            var options = string.IsNullOrWhiteSpace(request.NfsOptions) ? "rw,sync,no_subtree_check" : request.NfsOptions;
            
            var exportLine = $"{request.Path} {allowedHosts}({options})";
            
            // Append to exports file
            var lines = new List<string>();
            if (File.Exists(NfsExportsPath))
            {
                lines = (await File.ReadAllLinesAsync(NfsExportsPath)).ToList();
            }
            
            // Add comment if provided
            if (!string.IsNullOrWhiteSpace(request.Comment))
            {
                lines.Add($"# {request.Comment}");
            }
            
            lines.Add(exportLine);
            
            // Write configuration atomically
            await WriteConfigFileAsync(NfsExportsPath, lines);
            
            // Reload exports
            var reloadResult = await ReloadNfsExportsAsync();
            if (!reloadResult.Contains("Success", StringComparison.OrdinalIgnoreCase))
            {
                return $"Export added but failed to reload: {reloadResult}";
            }
            
            return $"Successfully added NFS export '{request.Path}' and reloaded exports.";
        }
        catch (UnauthorizedAccessException)
        {
            return "Permission denied. The application needs root privileges to modify NFS exports.";
        }
        catch (Exception ex)
        {
            return $"Error adding NFS export: {ex.Message}";
        }
    }
    
    public async Task<string> UpdateCifsShareAsync(string shareName, ShareRequest request)
    {
        // For CIFS, we need to remove the old share and add the new one
        var removeResult = await RemoveCifsShareAsync(shareName);
        if (!removeResult.Contains("Successfully", StringComparison.OrdinalIgnoreCase))
        {
            return removeResult;
        }
        
        return await AddCifsShareAsync(request);
    }
    
    public async Task<string> UpdateNfsShareAsync(string exportPath, ShareRequest request)
    {
        // For NFS, we need to remove the old export and add the new one
        var removeResult = await RemoveNfsShareAsync(exportPath);
        if (!removeResult.Contains("Successfully", StringComparison.OrdinalIgnoreCase))
        {
            return removeResult;
        }
        
        return await AddNfsShareAsync(request);
    }
    
    public async Task<string> RemoveCifsShareAsync(string shareName)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return "CIFS share management is only supported on Linux";
        }
        
        if (string.IsNullOrWhiteSpace(shareName) || InvalidChars.Any(c => shareName.Contains(c)))
        {
            return "Invalid share name";
        }
        
        try
        {
            if (!File.Exists(SambaConfigPath))
            {
                return "Samba configuration file not found";
            }
            
            var lines = await File.ReadAllLinesAsync(SambaConfigPath);
            var newLines = new List<string>();
            bool inTargetShare = false;
            bool shareFound = false;
            
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                
                // Check for share section header
                var shareMatch = ShareNameRegex.Match(trimmedLine);
                if (shareMatch.Success)
                {
                    var currentShareName = shareMatch.Groups[1].Value;
                    
                    if (currentShareName.Equals(shareName, StringComparison.OrdinalIgnoreCase))
                    {
                        inTargetShare = true;
                        shareFound = true;
                        continue; // Skip this line
                    }
                    else
                    {
                        inTargetShare = false;
                    }
                }
                
                // Skip lines within the target share section
                if (inTargetShare)
                {
                    continue;
                }
                
                newLines.Add(line);
            }
            
            if (!shareFound)
            {
                return $"Share '{shareName}' not found";
            }
            
            // Write configuration atomically
            await WriteConfigFileAsync(SambaConfigPath, newLines);
            
            return $"Successfully removed CIFS share '{shareName}'. Please restart Samba service to apply changes.";
        }
        catch (UnauthorizedAccessException)
        {
            return "Permission denied. The application needs root privileges to modify Samba configuration.";
        }
        catch (Exception ex)
        {
            return $"Error removing CIFS share: {ex.Message}";
        }
    }
    
    public async Task<string> RemoveNfsShareAsync(string exportPath)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return "NFS export management is only supported on Linux";
        }
        
        if (string.IsNullOrWhiteSpace(exportPath) || InvalidChars.Any(c => exportPath.Contains(c)))
        {
            return "Invalid export path";
        }
        
        try
        {
            if (!File.Exists(NfsExportsPath))
            {
                return "NFS exports file not found";
            }
            
            var lines = await File.ReadAllLinesAsync(NfsExportsPath);
            var newLines = new List<string>();
            bool exportFound = false;
            
            for (int i = 0; i < lines.Length; i++)
            {
                var trimmedLine = lines[i].Trim();
                
                // Check if this line is the export we want to remove
                // Use exact matching to avoid removing wrong exports (e.g., /srv/data vs /srv/data2)
                if (!string.IsNullOrWhiteSpace(trimmedLine) && 
                    !trimmedLine.StartsWith("#") &&
                    (trimmedLine.Equals(exportPath, StringComparison.Ordinal) ||
                     trimmedLine.StartsWith(exportPath + " ", StringComparison.Ordinal) ||
                     trimmedLine.StartsWith(exportPath + "\t", StringComparison.Ordinal)))
                {
                    exportFound = true;
                    
                    // Also skip the comment line before this export if it exists
                    if (newLines.Count > 0 && newLines[newLines.Count - 1].Trim().StartsWith("#"))
                    {
                        newLines.RemoveAt(newLines.Count - 1);
                    }
                    
                    continue; // Skip this export line
                }
                
                newLines.Add(lines[i]);
            }
            
            if (!exportFound)
            {
                return $"Export '{exportPath}' not found";
            }
            
            // Write configuration atomically
            await WriteConfigFileAsync(NfsExportsPath, newLines);
            
            // Reload exports
            var reloadResult = await ReloadNfsExportsAsync();
            if (!reloadResult.Contains("Success", StringComparison.OrdinalIgnoreCase))
            {
                return $"Export removed but failed to reload: {reloadResult}";
            }
            
            return $"Successfully removed NFS export '{exportPath}' and reloaded exports.";
        }
        catch (UnauthorizedAccessException)
        {
            return "Permission denied. The application needs root privileges to modify NFS exports.";
        }
        catch (Exception ex)
        {
            return $"Error removing NFS export: {ex.Message}";
        }
    }
    
    public async Task<string> RestartSambaServiceAsync()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return "Samba service management is only supported on Linux";
        }
        
        try
        {
            // Try systemctl first - restart both smbd and nmbd
            var resultSmbd = await ExecuteCommandAsync("systemctl", "restart smbd");
            var resultNmbd = await ExecuteCommandAsync("systemctl", "restart nmbd");
            
            if (resultSmbd.exitCode == 0 && resultNmbd.exitCode == 0)
            {
                return "Successfully restarted Samba service";
            }
            
            // Try service command as fallback
            resultSmbd = await ExecuteCommandAsync("service", "smbd restart");
            resultNmbd = await ExecuteCommandAsync("service", "nmbd restart");
            
            if (resultSmbd.exitCode == 0 && resultNmbd.exitCode == 0)
            {
                return "Successfully restarted Samba service";
            }
            
            return $"Failed to restart Samba service. smbd: {resultSmbd.error}, nmbd: {resultNmbd.error}";
        }
        catch (Exception ex)
        {
            return $"Error restarting Samba service: {ex.Message}";
        }
    }
    
    public async Task<string> RestartNfsServiceAsync()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return "NFS service management is only supported on Linux";
        }
        
        try
        {
            // Try systemctl first
            var nfsServerResult = await ExecuteCommandAsync("systemctl", "restart nfs-server");
            if (nfsServerResult.exitCode == 0)
            {
                return "Successfully restarted NFS service";
            }
            
            // Try nfs-kernel-server for Debian/Ubuntu
            var nfsKernelServerResult = await ExecuteCommandAsync("systemctl", "restart nfs-kernel-server");
            if (nfsKernelServerResult.exitCode == 0)
            {
                return "Successfully restarted NFS service";
            }
            
            return $"Failed to restart NFS service. nfs-server: {nfsServerResult.error}; nfs-kernel-server: {nfsKernelServerResult.error}";
        }
        catch (Exception ex)
        {
            return $"Error restarting NFS service: {ex.Message}";
        }
    }
    
    public async Task<string> TestSambaConfigAsync()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return "Samba configuration test is only supported on Linux";
        }
        
        try
        {
            var result = await ExecuteCommandAsync("testparm", "-s");
            if (result.exitCode == 0)
            {
                return "Samba configuration is valid";
            }
            
            return $"Samba configuration test failed: {result.error}";
        }
        catch (Exception ex)
        {
            return $"Error testing Samba configuration: {ex.Message}";
        }
    }
    
    private async Task<string> ReloadNfsExportsAsync()
    {
        try
        {
            var result = await ExecuteCommandAsync("exportfs", "-ra");
            if (result.exitCode == 0)
            {
                return "Successfully reloaded NFS exports";
            }
            
            return $"Failed to reload NFS exports: {result.error}";
        }
        catch (Exception ex)
        {
            return $"Error reloading NFS exports: {ex.Message}";
        }
    }
    
    private static (bool isValid, string message) ValidateShareRequest(ShareRequest request, ShareType type)
    {
        if (string.IsNullOrWhiteSpace(request.Name) && type == ShareType.CIFS)
        {
            return (false, "Share name is required");
        }
        
        if (string.IsNullOrWhiteSpace(request.Path))
        {
            return (false, "Share path is required");
        }
        
        if (!Path.IsPathRooted(request.Path))
        {
            return (false, "Share path must be an absolute path");
        }
        
        // Check for invalid characters in name (only if CIFS and name is provided)
        if (type == ShareType.CIFS && !string.IsNullOrEmpty(request.Name) && InvalidChars.Any(c => request.Name.Contains(c)))
        {
            return (false, "Invalid characters in share name");
        }
        
        // Check for invalid characters in path
        if (InvalidChars.Any(c => request.Path.Contains(c)))
        {
            return (false, "Invalid characters in share path");
        }
        
        return (true, string.Empty);
    }
    
    private static async Task WriteConfigFileAsync(string filePath, List<string> lines)
    {
        var tempPath = $"{filePath}.tmp";
        var backupPath = $"{filePath}.bak";
        var backupCreated = false;
        
        try
        {
            // Ensure the parent directory exists
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            // Write new configuration to a temporary file
            await File.WriteAllLinesAsync(tempPath, lines);
            
            // Verify the temp file was written successfully
            var tempInfo = new FileInfo(tempPath);
            if (!tempInfo.Exists || tempInfo.Length == 0)
            {
                throw new IOException($"Temporary configuration file '{tempPath}' was not written correctly.");
            }
            
            // Create a backup of the existing file, if any
            if (File.Exists(filePath))
            {
                File.Copy(filePath, backupPath, overwrite: true);
                backupCreated = true;
                File.Delete(filePath);
            }
            
            // Move the temp file into place
            File.Move(tempPath, filePath);
            
            // If we succeeded, remove the backup
            if (backupCreated && File.Exists(backupPath))
            {
                try { File.Delete(backupPath); } catch { }
            }
        }
        catch
        {
            // Attempt rollback using backup if available
            if (backupCreated && File.Exists(backupPath))
            {
                try
                {
                    if (File.Exists(filePath))
                    {
                        try { File.Delete(filePath); } catch { }
                    }
                    File.Move(backupPath, filePath);
                }
                catch (Exception rollbackEx)
                {
                    Debug.WriteLine($"Failed to rollback configuration file '{filePath}' from backup: {rollbackEx}");
                }
            }
            
            // Clean up temporary file if it exists
            if (File.Exists(tempPath))
            {
                try 
                { 
                    File.Delete(tempPath); 
                }
                catch (Exception cleanupEx)
                {
                    Debug.WriteLine($"Failed to delete temporary config file '{tempPath}': {cleanupEx}");
                }
            }
            throw;
        }
    }
    
    private static async Task<(int exitCode, string output, string error)> ExecuteCommandAsync(string command, string arguments)
    {
        var processInfo = new ProcessStartInfo
        {
            FileName = command,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = "/tmp"
        };
        
        using var process = Process.Start(processInfo);
        if (process == null)
        {
            return (-1, string.Empty, "Failed to start process");
        }
        
        await process.WaitForExitAsync();
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        
        return (process.ExitCode, output, error);
    }
    
    private static bool ParseYesNo(string value)
    {
        return value.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("1", StringComparison.OrdinalIgnoreCase);
    }
    
    // Feature detection implementation
    public async Task<ShareManagementFeatureDetection> DetectFeaturesAsync()
    {
        var detection = new ShareManagementFeatureDetection
        {
            Requirements = new List<FeatureRequirement>()
        };
        
        // Detect OS
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            detection.DetectedOS = "Linux";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            detection.DetectedOS = "Windows";
            detection.Summary = "共享目录管理功能主要设计用于 Linux 系统。Windows 系统功能有限。";
            detection.AllRequiredFeaturesAvailable = false;
            return detection;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            detection.DetectedOS = "macOS";
            detection.Summary = "共享目录管理功能主要设计用于 Linux 系统。macOS 系统功能有限。";
            detection.AllRequiredFeaturesAvailable = false;
            return detection;
        }
        else
        {
            detection.DetectedOS = "Unknown";
            detection.Summary = "无法识别操作系统类型。共享目录管理功能仅支持 Linux。";
            detection.AllRequiredFeaturesAvailable = false;
            return detection;
        }
        
        // Check for required tools on Linux
        var requirements = new List<(string name, string desc, bool required, string installUbuntu, string installRhel)>
        {
            ("smbd", "Samba 服务器守护进程 - 提供 CIFS/SMB 共享服务", false, 
             "sudo apt-get install samba", 
             "sudo yum install samba"),
            ("testparm", "Samba 配置测试工具 - 验证 smb.conf 配置文件语法", false, 
             "sudo apt-get install samba-common-bin", 
             "sudo yum install samba-common"),
            ("nfsd", "NFS 服务器守护进程 - 提供 NFS 共享服务", false, 
             "sudo apt-get install nfs-kernel-server", 
             "sudo yum install nfs-utils"),
            ("exportfs", "NFS 导出管理工具 - 管理 NFS 导出列表", false, 
             "sudo apt-get install nfs-kernel-server", 
             "sudo yum install nfs-utils")
        };
        
        // Check all tools in parallel for better performance
        var checkTasks = requirements.Select(async req =>
        {
            var (name, desc, required, installUbuntu, installRhel) = req;
            var status = await CheckCommandOrServiceAvailabilityAsync(name);
            var installCmd = $"Ubuntu/Debian: {installUbuntu}\nCentOS/RHEL/Fedora: {installRhel}";
            var checkCmd = GetCheckCommandForTool(name);
            
            return new FeatureRequirement
            {
                Name = name,
                Description = desc,
                Status = status ? FeatureStatus.Available : FeatureStatus.Missing,
                IsRequired = required,
                CheckCommand = checkCmd,
                InstallCommand = installCmd,
                Notes = required 
                    ? "此工具是必需的，没有它将无法使用基本共享管理功能" 
                    : "此工具是可选的，用于增强功能"
            };
        }).ToList();
        
        detection.Requirements = (await Task.WhenAll(checkTasks)).ToList();
        
        // All tools are optional, so this is always true unless OS is not Linux
        detection.AllRequiredFeaturesAvailable = true;
        
        // Generate summary
        var missingCifs = detection.Requirements
            .Where(r => (r.Name == "smbd" || r.Name == "testparm") && r.Status == FeatureStatus.Missing)
            .ToList();
        var missingNfs = detection.Requirements
            .Where(r => (r.Name == "nfsd" || r.Name == "exportfs") && r.Status == FeatureStatus.Missing)
            .ToList();
        
        var summaryParts = new List<string>();
        
        if (missingCifs.Any() && missingNfs.Any())
        {
            summaryParts.Add("未检测到 CIFS/Samba 和 NFS 服务。");
            summaryParts.Add($"CIFS 缺少：{string.Join(", ", missingCifs.Select(r => r.Name))}");
            summaryParts.Add($"NFS 缺少：{string.Join(", ", missingNfs.Select(r => r.Name))}");
        }
        else if (missingCifs.Any())
        {
            summaryParts.Add("NFS 服务可用。");
            summaryParts.Add($"CIFS/Samba 缺少：{string.Join(", ", missingCifs.Select(r => r.Name))}");
        }
        else if (missingNfs.Any())
        {
            summaryParts.Add("CIFS/Samba 服务可用。");
            summaryParts.Add($"NFS 缺少：{string.Join(", ", missingNfs.Select(r => r.Name))}");
        }
        else
        {
            summaryParts.Add("CIFS/Samba 和 NFS 服务均已安装并可用。");
        }
        
        detection.Summary = string.Join(" ", summaryParts);
        
        return detection;
    }
    
    private static string GetCheckCommandForTool(string name)
    {
        // Special handling for NFS: the kernel server is typically exposed as
        // nfs-server.service or nfs-kernel-server.service, not nfsd.service
        if (name.Equals("nfsd", StringComparison.OrdinalIgnoreCase))
        {
            return "systemctl list-unit-files nfs-server.service nfs-kernel-server.service";
        }
        
        return name.EndsWith("d") 
            ? $"systemctl list-unit-files {name}.service" 
            : $"which {name}";
    }
    
    private static async Task<bool> CheckSystemdServiceAvailabilityAsync(string serviceUnitName)
    {
        try
        {
            // Validate service name to prevent command injection
            if (string.IsNullOrWhiteSpace(serviceUnitName) || 
                !SafeNameRegex.IsMatch(serviceUnitName))
            {
                Debug.WriteLine($"Invalid service name: {serviceUnitName}");
                return false;
            }
            
            var processInfo = new ProcessStartInfo
            {
                FileName = "systemctl",
                Arguments = $"list-unit-files {serviceUnitName}.service",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process == null)
            {
                Debug.WriteLine($"Failed to start systemctl process for service: {serviceUnitName}");
                return false;
            }

            // Add timeout to prevent indefinite blocking
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10));
            var processTask = process.WaitForExitAsync();
            var completedTask = await Task.WhenAny(processTask, timeoutTask);
            
            if (completedTask == timeoutTask)
            {
                Debug.WriteLine($"Timeout waiting for systemctl to check service: {serviceUnitName}");
                try 
                { 
                    process.Kill(); 
                } 
                catch (Exception ex) 
                { 
                    Debug.WriteLine($"Failed to kill systemctl process for service '{serviceUnitName}': {ex.Message}");
                }
                return false;
            }
            
            var output = await process.StandardOutput.ReadToEndAsync();

            // Check if the service unit file exists
            return output.Contains($"{serviceUnitName}.service", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to check availability of service '{serviceUnitName}': {ex}");
            return false;
        }
    }
    
    private static async Task<bool> CheckCommandOrServiceAvailabilityAsync(string name)
    {
        // Special handling for NFS: check common NFS server unit names first
        if (name.Equals("nfsd", StringComparison.OrdinalIgnoreCase))
        {
            var nfsServiceNames = new[] { "nfs-server", "nfs-kernel-server" };
            foreach (var serviceName in nfsServiceNames)
            {
                if (await CheckSystemdServiceAvailabilityAsync(serviceName))
                {
                    return true;
                }
            }
            // If neither typical NFS server unit exists, return false
            return false;
        }
        
        // For services (ending with 'd'), check if they exist via systemctl
        if (name.EndsWith("d"))
        {
            return await CheckSystemdServiceAvailabilityAsync(name);
        }
        
        // For commands, use 'which'
        try
        {
            // Validate command name to prevent command injection
            if (string.IsNullOrWhiteSpace(name) || 
                !SafeNameRegex.IsMatch(name))
            {
                Debug.WriteLine($"Invalid command name: {name}");
                return false;
            }
            
            var processInfo = new ProcessStartInfo
            {
                FileName = "which",
                Arguments = name,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            using var process = Process.Start(processInfo);
            if (process == null)
            {
                Debug.WriteLine($"Failed to start which process for command: {name}");
                return false;
            }
            
            // Add timeout to prevent indefinite blocking
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10));
            var processTask = process.WaitForExitAsync();
            var completedTask = await Task.WhenAny(processTask, timeoutTask);
            
            if (completedTask == timeoutTask)
            {
                Debug.WriteLine($"Timeout waiting for which to check command: {name}");
                try 
                { 
                    process.Kill(); 
                } 
                catch (Exception ex) 
                { 
                    Debug.WriteLine($"Failed to kill which process for command '{name}': {ex.Message}");
                }
                return false;
            }
            
            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to check availability of command '{name}': {ex}");
            return false;
        }
    }
}
