namespace QingFeng.Models;

public class SystemResourceInfo
{
    public CpuInfo Cpu { get; set; } = new();
    public MemoryInfo Memory { get; set; } = new();
    public List<DiskInfo> Disks { get; set; } = new();
    public List<NetworkInfo> Networks { get; set; } = new();
}

public class CpuInfo
{
    public double UsagePercent { get; set; }
    public int CoreCount { get; set; }
    public string ProcessorName { get; set; } = string.Empty;
}

public class MemoryInfo
{
    public long TotalBytes { get; set; }
    public long UsedBytes { get; set; }
    public long AvailableBytes { get; set; }
    public double UsagePercent { get; set; }
    
    public string TotalGB => $"{TotalBytes / 1024.0 / 1024.0 / 1024.0:F2} GB";
    public string UsedGB => $"{UsedBytes / 1024.0 / 1024.0 / 1024.0:F2} GB";
    public string AvailableGB => $"{AvailableBytes / 1024.0 / 1024.0 / 1024.0:F2} GB";
}

public class DiskInfo
{
    public string Name { get; set; } = string.Empty;
    public string MountPoint { get; set; } = string.Empty;
    public string FileSystem { get; set; } = string.Empty;
    public long TotalBytes { get; set; }
    public long UsedBytes { get; set; }
    public long AvailableBytes { get; set; }
    public double UsagePercent { get; set; }
    public bool IsReady { get; set; }
    
    public string TotalGB => $"{TotalBytes / 1024.0 / 1024.0 / 1024.0:F2} GB";
    public string UsedGB => $"{UsedBytes / 1024.0 / 1024.0 / 1024.0:F2} GB";
    public string AvailableGB => $"{AvailableBytes / 1024.0 / 1024.0 / 1024.0:F2} GB";
}

public class NetworkInfo
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public long BytesSent { get; set; }
    public long BytesReceived { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<string> IpAddresses { get; set; } = new();
    
    public string SentMB => $"{BytesSent / 1024.0 / 1024.0:F2} MB";
    public string ReceivedMB => $"{BytesReceived / 1024.0 / 1024.0:F2} MB";
}
