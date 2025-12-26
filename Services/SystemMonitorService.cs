using QingFeng.Models;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace QingFeng.Services;

public class SystemMonitorService : ISystemMonitorService
{
    private DateTime _lastCpuCheck = DateTime.MinValue;
    private TimeSpan _lastTotalProcessorTime = TimeSpan.Zero;
    private double _lastCpuUsage = 0;
    
    // Fields for /proc/stat based CPU monitoring
    private long _lastTotalCpuTime = 0;
    private long _lastIdleCpuTime = 0;

    public async Task<SystemResourceInfo> GetSystemResourceInfoAsync()
    {
        return new SystemResourceInfo
        {
            Cpu = await GetCpuInfoAsync(),
            Memory = await GetMemoryInfoAsync(),
            Disks = await GetDiskInfoAsync(),
            Networks = await GetNetworkInfoAsync()
        };
    }

    public Task<CpuInfo> GetCpuInfoAsync()
    {
        var cpuInfo = new CpuInfo
        {
            CoreCount = Environment.ProcessorCount,
            ProcessorName = RuntimeInformation.ProcessArchitecture.ToString()
        };

        // Use /proc/stat for CPU usage on Linux
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            try
            {
                // Use ReadLines for memory efficiency - only read until we find the cpu line
                foreach (var line in File.ReadLines("/proc/stat"))
                {
                    if (line.StartsWith("cpu "))
                    {
                        var cpuUsage = ParseProcStatCpu(line);
                        cpuInfo.UsagePercent = Math.Round(cpuUsage, 2);
                        break;
                    }
                }
            }
            catch (FileNotFoundException)
            {
                // /proc/stat not available, fallback to process-based calculation
                CalculateCpuUsageFromProcess(cpuInfo);
            }
            catch (IOException)
            {
                // Error reading /proc/stat, fallback to process-based calculation
                CalculateCpuUsageFromProcess(cpuInfo);
            }
            catch (FormatException)
            {
                // Invalid data format in /proc/stat, fallback to process-based calculation
                CalculateCpuUsageFromProcess(cpuInfo);
            }
        }
        else
        {
            // For non-Linux platforms, use process-based calculation
            CalculateCpuUsageFromProcess(cpuInfo);
        }

        return Task.FromResult(cpuInfo);
    }

    private double ParseProcStatCpu(string cpuLine)
    {
        // CPU line format: cpu user nice system idle iowait irq softirq steal guest guest_nice
        var parts = cpuLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        
        if (parts.Length < 5)
        {
            return _lastCpuUsage;
        }

        // Parse CPU time values (all in USER_HZ units, typically 1/100th of a second)
        // Use TryParse for safer parsing
        if (!long.TryParse(parts[1], out long user) ||
            !long.TryParse(parts[2], out long nice) ||
            !long.TryParse(parts[3], out long system) ||
            !long.TryParse(parts[4], out long idle))
        {
            return _lastCpuUsage;
        }

        long iowait = parts.Length > 5 && long.TryParse(parts[5], out var iow) ? iow : 0;
        long irq = parts.Length > 6 && long.TryParse(parts[6], out var irqVal) ? irqVal : 0;
        long softirq = parts.Length > 7 && long.TryParse(parts[7], out var sirq) ? sirq : 0;
        long steal = parts.Length > 8 && long.TryParse(parts[8], out var stl) ? stl : 0;

        // Calculate total and idle times
        long totalTime = user + nice + system + idle + iowait + irq + softirq + steal;
        long idleTime = idle + iowait;

        // Calculate CPU usage percentage
        double cpuUsage = 0;
        if (_lastTotalCpuTime != 0)
        {
            long totalDiff = totalTime - _lastTotalCpuTime;
            long idleDiff = idleTime - _lastIdleCpuTime;

            if (totalDiff > 0)
            {
                cpuUsage = ((double)(totalDiff - idleDiff) / totalDiff) * 100;
            }
        }

        // Store current values for next calculation
        _lastTotalCpuTime = totalTime;
        _lastIdleCpuTime = idleTime;
        _lastCpuUsage = cpuUsage;

        return cpuUsage;
    }

    private void CalculateCpuUsageFromProcess(CpuInfo cpuInfo)
    {
        // Calculate CPU usage based on current process
        var now = DateTime.UtcNow;
        var totalProcessorTime = Process.GetCurrentProcess().TotalProcessorTime;

        if (_lastCpuCheck != DateTime.MinValue)
        {
            var timeDiff = (now - _lastCpuCheck).TotalMilliseconds;
            var processorTimeDiff = (totalProcessorTime - _lastTotalProcessorTime).TotalMilliseconds;
            
            if (timeDiff > 0)
            {
                _lastCpuUsage = (processorTimeDiff / (Environment.ProcessorCount * timeDiff)) * 100;
            }
        }

        _lastCpuCheck = now;
        _lastTotalProcessorTime = totalProcessorTime;

        cpuInfo.UsagePercent = Math.Round(_lastCpuUsage, 2);
    }

    public Task<MemoryInfo> GetMemoryInfoAsync()
    {
        var memInfo = new MemoryInfo();
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            try
            {
                var lines = File.ReadAllLines("/proc/meminfo");
                long memTotal = 0, memAvailable = 0;

                foreach (var line in lines)
                {
                    if (line.StartsWith("MemTotal:"))
                    {
                        memTotal = ParseMemInfoValue(line);
                    }
                    else if (line.StartsWith("MemAvailable:"))
                    {
                        memAvailable = ParseMemInfoValue(line);
                    }
                }

                memInfo.TotalBytes = memTotal * 1024; // Convert KB to bytes
                memInfo.AvailableBytes = memAvailable * 1024;
                memInfo.UsedBytes = memInfo.TotalBytes - memInfo.AvailableBytes;
                memInfo.UsagePercent = memInfo.TotalBytes > 0 
                    ? Math.Round((double)memInfo.UsedBytes / memInfo.TotalBytes * 100, 2) 
                    : 0;
            }
            catch
            {
                // Fallback to GC info
                SetMemoryInfoFromGC(memInfo);
            }
        }
        else
        {
            // For Windows and other platforms, use GC info
            SetMemoryInfoFromGC(memInfo);
        }

        return Task.FromResult(memInfo);
    }

    private void SetMemoryInfoFromGC(MemoryInfo memInfo)
    {
        var gcMemory = GC.GetTotalMemory(false);
        memInfo.UsedBytes = gcMemory;
        memInfo.TotalBytes = gcMemory * 2; // Rough estimate
        memInfo.AvailableBytes = memInfo.TotalBytes - memInfo.UsedBytes;
        memInfo.UsagePercent = 50; // Rough estimate
    }

    private long ParseMemInfoValue(string line)
    {
        var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2 && long.TryParse(parts[1], out var value))
        {
            return value;
        }
        return 0;
    }

    public Task<List<DiskInfo>> GetDiskInfoAsync()
    {
        var disks = new List<DiskInfo>();

        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (drive.IsReady)
                {
                    var diskInfo = new DiskInfo
                    {
                        Name = drive.Name,
                        MountPoint = drive.RootDirectory.FullName,
                        FileSystem = drive.DriveFormat,
                        TotalBytes = drive.TotalSize,
                        AvailableBytes = drive.AvailableFreeSpace,
                        UsedBytes = drive.TotalSize - drive.AvailableFreeSpace,
                        IsReady = true
                    };
                    
                    diskInfo.UsagePercent = diskInfo.TotalBytes > 0 
                        ? Math.Round((double)diskInfo.UsedBytes / diskInfo.TotalBytes * 100, 2) 
                        : 0;
                    
                    disks.Add(diskInfo);
                }
                else
                {
                    disks.Add(new DiskInfo
                    {
                        Name = drive.Name,
                        MountPoint = drive.RootDirectory.FullName,
                        IsReady = false
                    });
                }
            }
            catch
            {
                // Skip drives that throw exceptions
                continue;
            }
        }

        return Task.FromResult(disks);
    }

    public Task<List<NetworkInfo>> GetNetworkInfoAsync()
    {
        var networks = new List<NetworkInfo>();

        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            try
            {
                var stats = nic.GetIPv4Statistics();
                var ipProps = nic.GetIPProperties();
                var addresses = ipProps.UnicastAddresses
                    .Select(addr => addr.Address.ToString())
                    .ToList();

                var networkInfo = new NetworkInfo
                {
                    Name = nic.Name,
                    Description = nic.Description,
                    BytesSent = stats.BytesSent,
                    BytesReceived = stats.BytesReceived,
                    Status = nic.OperationalStatus.ToString(),
                    IpAddresses = addresses
                };

                networks.Add(networkInfo);
            }
            catch
            {
                // Skip network interfaces that throw exceptions
                continue;
            }
        }

        return Task.FromResult(networks);
    }
}
