using QingFeng.Models;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace QingFeng.Services;

public class NetworkManagementService : INetworkManagementService
{
    private readonly ILogger<NetworkManagementService> _logger;

    public NetworkManagementService(ILogger<NetworkManagementService> logger)
    {
        _logger = logger;
    }

    public async Task<List<NetworkInterfaceInfo>> GetNetworkInterfacesAsync()
    {
        var interfaces = new List<NetworkInterfaceInfo>();

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                interfaces = await GetLinuxNetworkInterfacesAsync();
            }
            else
            {
                // Fallback to .NET API for non-Linux platforms
                interfaces = GetDotNetNetworkInterfaces();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting network interfaces");
        }

        return interfaces;
    }

    public async Task<NetworkInterfaceInfo?> GetNetworkInterfaceAsync(string interfaceName)
    {
        var interfaces = await GetNetworkInterfacesAsync();
        return interfaces.FirstOrDefault(i => i.Name == interfaceName);
    }

    public async Task<bool> SetStaticIpAsync(string interfaceName, string ipAddress, string netmask, string? gateway = null, string? dns = null)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            _logger.LogWarning("Network configuration is only supported on Linux");
            return false;
        }

        try
        {
            // Try to use nmcli (NetworkManager) first
            var nmcliAvailable = await CheckCommandAvailableAsync("nmcli");
            if (nmcliAvailable)
            {
                return await SetStaticIpWithNmcliAsync(interfaceName, ipAddress, netmask, gateway, dns);
            }

            // Fallback to ip command
            _logger.LogWarning("NetworkManager not available, using ip command (temporary configuration)");
            return await SetStaticIpWithIpCommandAsync(interfaceName, ipAddress, netmask, gateway);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting static IP for interface {Interface}", interfaceName);
            return false;
        }
    }

    public async Task<bool> SetDhcpAsync(string interfaceName)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            _logger.LogWarning("Network configuration is only supported on Linux");
            return false;
        }

        try
        {
            // Try to use nmcli (NetworkManager) first
            var nmcliAvailable = await CheckCommandAvailableAsync("nmcli");
            if (nmcliAvailable)
            {
                return await SetDhcpWithNmcliAsync(interfaceName);
            }

            // Fallback to dhclient
            _logger.LogWarning("NetworkManager not available, using dhclient");
            return await SetDhcpWithDhclientAsync(interfaceName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting DHCP for interface {Interface}", interfaceName);
            return false;
        }
    }

    private async Task<List<NetworkInterfaceInfo>> GetLinuxNetworkInterfacesAsync()
    {
        var interfaces = new List<NetworkInterfaceInfo>();

        try
        {
            // Use ip command to get interface information
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ip",
                    Arguments = "-details address show",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                interfaces = ParseIpAddressOutput(output);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing ip command");
        }

        return interfaces;
    }

    private List<NetworkInterfaceInfo> ParseIpAddressOutput(string output)
    {
        var interfaces = new List<NetworkInterfaceInfo>();
        var lines = output.Split('\n');
        NetworkInterfaceInfo? currentInterface = null;

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();

            // New interface line (e.g., "2: eth0: <BROADCAST,MULTICAST,UP,LOWER_UP>")
            if (Regex.IsMatch(trimmedLine, @"^\d+:\s+\w+:"))
            {
                if (currentInterface != null)
                {
                    interfaces.Add(currentInterface);
                }

                var parts = trimmedLine.Split(new[] { ':', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    currentInterface = new NetworkInterfaceInfo
                    {
                        Name = parts[1],
                        IsUp = trimmedLine.Contains("UP") && !trimmedLine.Contains("LOWER_UP") || trimmedLine.Contains("state UP")
                    };
                }
            }
            // MAC address line (e.g., "link/ether 00:0c:29:xx:xx:xx")
            else if (trimmedLine.StartsWith("link/ether") && currentInterface != null)
            {
                var parts = trimmedLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    currentInterface.MacAddress = parts[1];
                }
            }
            // IP address line (e.g., "inet 192.168.1.100/24")
            else if (trimmedLine.StartsWith("inet ") && currentInterface != null)
            {
                var parts = trimmedLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    var ipWithMask = parts[1];
                    var ipParts = ipWithMask.Split('/');
                    currentInterface.IpAddress = ipParts[0];
                    
                    if (ipParts.Length > 1 && int.TryParse(ipParts[1], out var prefix))
                    {
                        currentInterface.Netmask = PrefixToNetmask(prefix);
                    }
                }
            }
        }

        if (currentInterface != null)
        {
            interfaces.Add(currentInterface);
        }

        // Filter out loopback
        return interfaces.Where(i => i.Name != "lo").ToList();
    }

    private List<NetworkInterfaceInfo> GetDotNetNetworkInterfaces()
    {
        var interfaces = new List<NetworkInterfaceInfo>();

        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                continue;

            var ipProps = nic.GetIPProperties();
            var ipv4Address = ipProps.UnicastAddresses
                .FirstOrDefault(addr => addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

            var gateway = ipProps.GatewayAddresses
                .FirstOrDefault(g => g.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

            var interfaceInfo = new NetworkInterfaceInfo
            {
                Name = nic.Name,
                Description = nic.Description,
                Status = nic.OperationalStatus.ToString(),
                IpAddress = ipv4Address?.Address.ToString() ?? "",
                Netmask = ipv4Address?.IPv4Mask.ToString() ?? "",
                Gateway = gateway?.Address.ToString() ?? "",
                MacAddress = nic.GetPhysicalAddress().ToString(),
                IsUp = nic.OperationalStatus == OperationalStatus.Up,
                IsDhcp = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) 
                    ? (ipProps.GetIPv4Properties()?.IsDhcpEnabled ?? false)
                    : false
            };

            interfaces.Add(interfaceInfo);
        }

        return interfaces;
    }

    private async Task<bool> CheckCommandAvailableAsync(string command)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "which",
                    Arguments = command,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> SetStaticIpWithNmcliAsync(string interfaceName, string ipAddress, string netmask, string? gateway, string? dns)
    {
        try
        {
            // Calculate prefix from netmask
            var prefix = NetmaskToPrefix(netmask);
            var ipWithPrefix = $"{ipAddress}/{prefix}";

            // Get connection name for the interface
            var connectionName = await GetNmcliConnectionNameAsync(interfaceName);
            if (string.IsNullOrEmpty(connectionName))
            {
                _logger.LogWarning("No NetworkManager connection found for interface {Interface}", interfaceName);
                return false;
            }

            // Modify connection to use static IP
            var args = $"connection modify \"{connectionName}\" ipv4.method manual ipv4.addresses {ipWithPrefix}";
            
            if (!string.IsNullOrEmpty(gateway))
            {
                args += $" ipv4.gateway {gateway}";
            }
            
            if (!string.IsNullOrEmpty(dns))
            {
                args += $" ipv4.dns \"{dns}\"";
            }

            var success = await ExecuteCommandAsync("nmcli", args);
            
            if (success)
            {
                // Bring the connection up to apply changes
                await ExecuteCommandAsync("nmcli", $"connection up \"{connectionName}\"");
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting static IP with nmcli");
            return false;
        }
    }

    private async Task<bool> SetDhcpWithNmcliAsync(string interfaceName)
    {
        try
        {
            var connectionName = await GetNmcliConnectionNameAsync(interfaceName);
            if (string.IsNullOrEmpty(connectionName))
            {
                _logger.LogWarning("No NetworkManager connection found for interface {Interface}", interfaceName);
                return false;
            }

            // Modify connection to use DHCP
            var success = await ExecuteCommandAsync("nmcli", $"connection modify \"{connectionName}\" ipv4.method auto");
            
            if (success)
            {
                // Bring the connection up to apply changes
                await ExecuteCommandAsync("nmcli", $"connection up \"{connectionName}\"");
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting DHCP with nmcli");
            return false;
        }
    }

    private async Task<string?> GetNmcliConnectionNameAsync(string interfaceName)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "nmcli",
                    Arguments = $"-t -f NAME,DEVICE connection show --active",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    var parts = line.Split(':');
                    if (parts.Length >= 2 && parts[1].Trim() == interfaceName)
                    {
                        return parts[0].Trim();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting NetworkManager connection name");
        }

        return null;
    }

    private async Task<bool> SetStaticIpWithIpCommandAsync(string interfaceName, string ipAddress, string netmask, string? gateway)
    {
        try
        {
            var prefix = NetmaskToPrefix(netmask);
            
            // Flush existing IP addresses
            await ExecuteCommandAsync("ip", $"addr flush dev {interfaceName}");
            
            // Add new IP address
            var success = await ExecuteCommandAsync("ip", $"addr add {ipAddress}/{prefix} dev {interfaceName}");
            
            if (success && !string.IsNullOrEmpty(gateway))
            {
                // Add default gateway
                await ExecuteCommandAsync("ip", $"route add default via {gateway} dev {interfaceName}");
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting static IP with ip command");
            return false;
        }
    }

    private async Task<bool> SetDhcpWithDhclientAsync(string interfaceName)
    {
        try
        {
            // Release current lease
            await ExecuteCommandAsync("dhclient", $"-r {interfaceName}");
            
            // Request new lease
            return await ExecuteCommandAsync("dhclient", interfaceName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting DHCP with dhclient");
            return false;
        }
    }

    private async Task<bool> ExecuteCommandAsync(string command, string arguments)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                _logger.LogWarning("Command '{Command} {Arguments}' failed with exit code {ExitCode}: {Error}",
                    command, arguments, process.ExitCode, error);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing command '{Command} {Arguments}'", command, arguments);
            return false;
        }
    }

    private static string PrefixToNetmask(int prefix)
    {
        var mask = 0xFFFFFFFF << (32 - prefix);
        return $"{(mask >> 24) & 0xFF}.{(mask >> 16) & 0xFF}.{(mask >> 8) & 0xFF}.{mask & 0xFF}";
    }

    private static int NetmaskToPrefix(string netmask)
    {
        var parts = netmask.Split('.');
        if (parts.Length != 4)
            return 24; // Default to /24

        var binary = "";
        foreach (var part in parts)
        {
            if (int.TryParse(part, out var value))
            {
                binary += Convert.ToString(value, 2).PadLeft(8, '0');
            }
        }

        return binary.Count(c => c == '1');
    }
}
