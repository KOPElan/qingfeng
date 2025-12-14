using QingFeng.Models;

namespace QingFeng.Services;

public interface ISystemMonitorService
{
    Task<SystemResourceInfo> GetSystemResourceInfoAsync();
    Task<CpuInfo> GetCpuInfoAsync();
    Task<MemoryInfo> GetMemoryInfoAsync();
    Task<List<DiskInfo>> GetDiskInfoAsync();
    Task<List<NetworkInfo>> GetNetworkInfoAsync();
}
