using QingFeng.Models;

namespace QingFeng.Services;

public interface IDiskManagementService
{
    Task<List<DiskInfo>> GetAllDisksAsync();
    Task<List<DiskInfo>> GetAllBlockDevicesAsync();
    Task<DiskInfo?> GetDiskInfoAsync(string devicePath);
    Task<DiskOperationResult> MountDiskAsync(string devicePath, string mountPoint, string? fileSystem = null, string? options = null);
    Task<DiskOperationResult> MountDiskPermanentAsync(string devicePath, string mountPoint, string? fileSystem = null, string? options = null);
    Task<DiskOperationResult> UnmountDiskAsync(string mountPoint);
    Task<List<string>> GetSharesAsync();
    Task<List<string>> GetAvailableFileSystemsAsync();
    Task<DiskOperationResult> SetDiskSpinDownAsync(string devicePath, int timeoutMinutes);
    Task<DiskOperationResult> SetDiskApmLevelAsync(string devicePath, int level);
    Task<PowerStatusResult> GetDiskPowerStatusAsync(string devicePath);
    Task<DiskPowerSettings> GetDiskPowerSettingsAsync(string devicePath);
    
    // Network disk management
    Task<List<NetworkDiskInfo>> GetNetworkDisksAsync();
    Task<DiskOperationResult> MountNetworkDiskAsync(string server, string sharePath, string mountPoint, NetworkDiskType diskType, string? username = null, string? password = null, string? domain = null, string? options = null);
    Task<DiskOperationResult> MountNetworkDiskPermanentAsync(string server, string sharePath, string mountPoint, NetworkDiskType diskType, string? username = null, string? password = null, string? domain = null, string? options = null);
    
    // Feature detection
    Task<DiskManagementFeatureDetection> DetectFeaturesAsync();
}
