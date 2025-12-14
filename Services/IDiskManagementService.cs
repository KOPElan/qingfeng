using QingFeng.Models;

namespace QingFeng.Services;

public interface IDiskManagementService
{
    Task<List<DiskInfo>> GetAllDisksAsync();
    Task<string> MountDiskAsync(string devicePath, string mountPoint);
    Task<string> UnmountDiskAsync(string mountPoint);
    Task<List<string>> GetSharesAsync();
}
