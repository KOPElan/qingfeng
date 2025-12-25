using QingFeng.Models;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace QingFeng.Services;

public class ShareManagementService : IShareManagementService
{
    private const string SambaConfigPath = "/etc/samba/smb.conf";
    private const string NfsExportsPath = "/etc/exports";
    
    private static readonly string[] InvalidChars = ["&&", ";", "|", "`", "$", "(", ")", "<", ">", "\"", "'", "\n", "\r", "\t"];
    private static readonly Regex ShareNameRegex = new(@"^\[([^\]]+)\]$", RegexOptions.Compiled);
    private static readonly Regex ParameterRegex = new(@"^\s*([a-zA-Z0-9_\-\s]+?)\s*=\s*(.+?)\s*$", RegexOptions.Compiled);
    
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
                var hostOptionRegex = new Regex(@"([^\s(]+)\(([^)]+)\)", RegexOptions.Compiled);
                var matches = hostOptionRegex.Matches(hostsAndOptions);
                
                foreach (Match match in matches)
                {
                    if (match.Groups.Count >= 3)
                    {
                        allowedHosts.Add(match.Groups[1].Value);
                        options.Add(match.Groups[2].Value);
                    }
                }
                
                var share = new ShareInfo
                {
                    Name = Path.GetFileName(exportPath) ?? exportPath,
                    Path = exportPath,
                    Type = ShareType.NFS,
                    AllowedHosts = allowedHosts.Count > 0 ? string.Join(", ", allowedHosts) : "*",
                    NfsOptions = options.Count > 0 ? options[0] : "rw,sync,no_subtree_check",
                    ReadOnly = options.Count > 0 && options[0].Contains("ro")
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
            var shareConfig = new StringBuilder();
            shareConfig.AppendLine();
            shareConfig.AppendLine($"[{request.Name}]");
            shareConfig.AppendLine($"   path = {request.Path}");
            
            if (!string.IsNullOrWhiteSpace(request.Comment))
            {
                shareConfig.AppendLine($"   comment = {request.Comment}");
            }
            
            shareConfig.AppendLine($"   browseable = {(request.Browseable ? "yes" : "no")}");
            shareConfig.AppendLine($"   read only = {(request.ReadOnly ? "yes" : "no")}");
            shareConfig.AppendLine($"   guest ok = {(request.GuestOk ? "yes" : "no")}");
            
            if (!string.IsNullOrWhiteSpace(request.ValidUsers))
            {
                shareConfig.AppendLine($"   valid users = {request.ValidUsers}");
            }
            
            if (!string.IsNullOrWhiteSpace(request.WriteList) && request.ReadOnly)
            {
                shareConfig.AppendLine($"   write list = {request.WriteList}");
            }
            
            // Append to configuration file
            lines.Add(shareConfig.ToString());
            
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
        if (!removeResult.Contains("Success", StringComparison.OrdinalIgnoreCase))
        {
            return removeResult;
        }
        
        return await AddCifsShareAsync(request);
    }
    
    public async Task<string> UpdateNfsShareAsync(string exportPath, ShareRequest request)
    {
        // For NFS, we need to remove the old export and add the new one
        var removeResult = await RemoveNfsShareAsync(exportPath);
        if (!removeResult.Contains("Success", StringComparison.OrdinalIgnoreCase))
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
                if (!string.IsNullOrWhiteSpace(trimmedLine) && 
                    !trimmedLine.StartsWith("#") &&
                    trimmedLine.StartsWith(exportPath))
                {
                    exportFound = true;
                    
                    // Also skip the comment line before this export if it exists
                    if (newLines.Count > 0 && newLines[^1].Trim().StartsWith("#"))
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
            // Try systemctl first
            var result = await ExecuteCommandAsync("systemctl", "restart smbd nmbd");
            if (result.exitCode == 0)
            {
                return "Successfully restarted Samba service";
            }
            
            // Try service command as fallback
            result = await ExecuteCommandAsync("service", "smbd restart && service nmbd restart");
            if (result.exitCode == 0)
            {
                return "Successfully restarted Samba service";
            }
            
            return $"Failed to restart Samba service: {result.error}";
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
            var result = await ExecuteCommandAsync("systemctl", "restart nfs-server");
            if (result.exitCode == 0)
            {
                return "Successfully restarted NFS service";
            }
            
            // Try nfs-kernel-server for Debian/Ubuntu
            result = await ExecuteCommandAsync("systemctl", "restart nfs-kernel-server");
            if (result.exitCode == 0)
            {
                return "Successfully restarted NFS service";
            }
            
            return $"Failed to restart NFS service: {result.error}";
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
        
        if (InvalidChars.Any(c => request.Name.Contains(c) || request.Path.Contains(c)))
        {
            return (false, "Invalid characters in share name or path");
        }
        
        return (true, string.Empty);
    }
    
    private static async Task WriteConfigFileAsync(string filePath, List<string> lines)
    {
        var tempPath = $"{filePath}.tmp";
        try
        {
            await File.WriteAllLinesAsync(tempPath, lines);
            File.Move(tempPath, filePath, overwrite: true);
        }
        catch
        {
            // Clean up temporary file if it exists
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { }
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
            CreateNoWindow = true
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
}
