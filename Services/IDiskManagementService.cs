using QingFeng.Models;

namespace QingFeng.Services;

public interface IDiskManagementService
{
    Task<List<DiskInfo>> GetAllDisksAsync();
    Task<List<DiskInfo>> GetAllBlockDevicesAsync();
    Task<DiskInfo?> GetDiskInfoAsync(string devicePath);
    Task<string> MountDiskAsync(string devicePath, string mountPoint, string? fileSystem = null, string? options = null);
    Task<string> MountDiskPermanentAsync(string devicePath, string mountPoint, string? fileSystem = null, string? options = null);
    Task<string> UnmountDiskAsync(string mountPoint);
    Task<List<string>> GetSharesAsync();
    Task<List<string>> GetAvailableFileSystemsAsync();
    Task<string> SetDiskSpinDownAsync(string devicePath, int timeoutMinutes);
    Task<string> SetDiskApmLevelAsync(string devicePath, int level);
    Task<string> GetDiskPowerStatusAsync(string devicePath);
}
