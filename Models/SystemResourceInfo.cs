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
    
    // Enhanced properties for advanced disk management
    public string DevicePath { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // disk, part, loop, etc.
    public string UUID { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool IsRemovable { get; set; }
    public bool IsReadOnly { get; set; }
    public string Model { get; set; } = string.Empty;
    public string Serial { get; set; } = string.Empty;
    public List<DiskInfo> Children { get; set; } = new();
    
    // Power management
    public bool? IsSpinningDown { get; set; }
    public int? ApmLevel { get; set; }
    
    public string TotalGB => $"{TotalBytes / 1024.0 / 1024.0 / 1024.0:F2} GB";
    public string UsedGB => $"{UsedBytes / 1024.0 / 1024.0 / 1024.0:F2} GB";
    public string AvailableGB => $"{AvailableBytes / 1024.0 / 1024.0 / 1024.0:F2} GB";
    public string SizeDisplay => TotalBytes > 0 ? TotalGB : "N/A";
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

public enum NetworkDiskType
{
    CIFS,
    NFS
}

public class NetworkDiskInfo
{
    public string Server { get; set; } = string.Empty;
    public string SharePath { get; set; } = string.Empty;
    public string MountPoint { get; set; } = string.Empty;
    public NetworkDiskType DiskType { get; set; }
    public string FileSystem { get; set; } = string.Empty; // cifs, nfs, nfs4, etc.
    public long TotalBytes { get; set; }
    public long UsedBytes { get; set; }
    public long AvailableBytes { get; set; }
    public double UsagePercent { get; set; }
    public bool IsReady { get; set; }
    public string Options { get; set; } = string.Empty;
    
    public string TotalGB => $"{TotalBytes / 1024.0 / 1024.0 / 1024.0:F2} GB";
    public string UsedGB => $"{UsedBytes / 1024.0 / 1024.0 / 1024.0:F2} GB";
    public string AvailableGB => $"{AvailableBytes / 1024.0 / 1024.0 / 1024.0:F2} GB";
    public string SizeDisplay => TotalBytes > 0 ? TotalGB : "N/A";
    public string FullPath => DiskType == NetworkDiskType.NFS
        ? $"{Server}:/{SharePath}"
        : $"//{Server}/{SharePath}";
}

public class DiskPowerSettings
{
    /// <summary>
    /// Spindown timeout in minutes. 0 means disabled.
    /// </summary>
    public int SpinDownTimeoutMinutes { get; set; }
    
    /// <summary>
    /// APM (Advanced Power Management) level. 1-255, where 1 = minimum power, 255 = maximum performance.
    /// null if not set.
    /// </summary>
    public int? ApmLevel { get; set; }
}
