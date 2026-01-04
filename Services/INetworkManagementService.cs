using QingFeng.Models;

namespace QingFeng.Services;

public interface INetworkManagementService
{
    /// <summary>
    /// Get all network interfaces
    /// </summary>
    Task<List<NetworkInterfaceInfo>> GetNetworkInterfacesAsync();
    
    /// <summary>
    /// Set static IP address for a network interface
    /// </summary>
    Task<bool> SetStaticIpAsync(string interfaceName, string ipAddress, string netmask, string? gateway = null, string? dns = null);
    
    /// <summary>
    /// Set DHCP for a network interface
    /// </summary>
    Task<bool> SetDhcpAsync(string interfaceName);
    
    /// <summary>
    /// Get detailed information about a specific network interface
    /// </summary>
    Task<NetworkInterfaceInfo?> GetNetworkInterfaceAsync(string interfaceName);
}
