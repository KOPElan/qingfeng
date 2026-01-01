using QingFeng.Services;
using QingFeng.Models;

namespace QingFeng.Endpoints;

public static class DiskManagementEndpoints
{
    public static void MapDiskManagementEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/disks")
            .WithTags("Disk Management");

        group.MapGet("", async (IDiskManagementService service) =>
        {
            try
            {
                var disks = await service.GetAllDisksAsync();
                return Results.Ok(disks);
            }
            catch (Exception ex)
            {
                return Results.Problem($"获取磁盘列表失败: {ex.Message}");
            }
        })
        .WithName("GetAllDisks")
        .WithSummary("获取所有磁盘");

        group.MapGet("/block-devices", async (IDiskManagementService service) =>
        {
            try
            {
                var devices = await service.GetAllBlockDevicesAsync();
                return Results.Ok(devices);
            }
            catch (Exception ex)
            {
                return Results.Problem($"获取块设备列表失败: {ex.Message}");
            }
        })
        .WithName("GetBlockDevices")
        .WithSummary("获取所有块设备");

        group.MapGet("/{devicePath}", async (string devicePath, IDiskManagementService service) =>
        {
            try
            {
                var disk = await service.GetDiskInfoAsync(devicePath);
                if (disk == null)
                {
                    return Results.NotFound();
                }
                return Results.Ok(disk);
            }
            catch (Exception ex)
            {
                return Results.Problem($"获取磁盘信息失败: {ex.Message}");
            }
        })
        .WithName("GetDiskInfo")
        .WithSummary("获取磁盘信息");

        group.MapPost("/mount", async (MountDiskRequest request, IDiskManagementService service) =>
        {
            try
            {
                var result = await service.MountDiskAsync(
                    request.DevicePath, 
                    request.MountPoint, 
                    request.FileSystem, 
                    request.Options);
                return Results.Ok(new { message = "挂载成功", result });
            }
            catch (Exception ex)
            {
                return Results.Problem($"挂载磁盘失败: {ex.Message}");
            }
        })
        .WithName("MountDisk")
        .WithSummary("挂载磁盘");

        group.MapPost("/mount-permanent", async (MountDiskRequest request, IDiskManagementService service) =>
        {
            try
            {
                var result = await service.MountDiskPermanentAsync(
                    request.DevicePath, 
                    request.MountPoint, 
                    request.FileSystem, 
                    request.Options);
                return Results.Ok(new { message = "永久挂载成功", result });
            }
            catch (Exception ex)
            {
                return Results.Problem($"永久挂载磁盘失败: {ex.Message}");
            }
        })
        .WithName("MountDiskPermanent")
        .WithSummary("永久挂载磁盘");

        group.MapPost("/unmount", async (UnmountDiskRequest request, IDiskManagementService service) =>
        {
            try
            {
                var result = await service.UnmountDiskAsync(request.MountPoint);
                return Results.Ok(new { message = "卸载成功", result });
            }
            catch (Exception ex)
            {
                return Results.Problem($"卸载磁盘失败: {ex.Message}");
            }
        })
        .WithName("UnmountDisk")
        .WithSummary("卸载磁盘");

        group.MapGet("/filesystems", async (IDiskManagementService service) =>
        {
            try
            {
                var filesystems = await service.GetAvailableFileSystemsAsync();
                return Results.Ok(filesystems);
            }
            catch (Exception ex)
            {
                return Results.Problem($"获取文件系统列表失败: {ex.Message}");
            }
        })
        .WithName("GetFileSystems")
        .WithSummary("获取可用文件系统");

        group.MapPost("/{devicePath}/spindown", async (string devicePath, SetSpinDownRequest request, IDiskManagementService service) =>
        {
            try
            {
                var result = await service.SetDiskSpinDownAsync(devicePath, request.TimeoutMinutes);
                return Results.Ok(new { message = "设置降速成功", result });
            }
            catch (Exception ex)
            {
                return Results.Problem($"设置降速失败: {ex.Message}");
            }
        })
        .WithName("SetDiskSpinDown")
        .WithSummary("设置磁盘降速");

        group.MapPost("/{devicePath}/apm", async (string devicePath, SetApmLevelRequest request, IDiskManagementService service) =>
        {
            try
            {
                var result = await service.SetDiskApmLevelAsync(devicePath, request.Level);
                return Results.Ok(new { message = "设置APM级别成功", result });
            }
            catch (Exception ex)
            {
                return Results.Problem($"设置APM级别失败: {ex.Message}");
            }
        })
        .WithName("SetApmLevel")
        .WithSummary("设置APM级别");

        group.MapGet("/{devicePath}/power-status", async (string devicePath, IDiskManagementService service) =>
        {
            try
            {
                var status = await service.GetDiskPowerStatusAsync(devicePath);
                return Results.Ok(new { status });
            }
            catch (Exception ex)
            {
                return Results.Problem($"获取电源状态失败: {ex.Message}");
            }
        })
        .WithName("GetPowerStatus")
        .WithSummary("获取电源状态");

        group.MapGet("/{devicePath}/power-settings", async (string devicePath, IDiskManagementService service) =>
        {
            try
            {
                var settings = await service.GetDiskPowerSettingsAsync(devicePath);
                return Results.Ok(settings);
            }
            catch (Exception ex)
            {
                return Results.Problem($"获取电源设置失败: {ex.Message}");
            }
        })
        .WithName("GetPowerSettings")
        .WithSummary("获取电源设置");

        // Network disk endpoints
        group.MapGet("/network", async (IDiskManagementService service) =>
        {
            try
            {
                var disks = await service.GetNetworkDisksAsync();
                return Results.Ok(disks);
            }
            catch (Exception ex)
            {
                return Results.Problem($"获取网络磁盘列表失败: {ex.Message}");
            }
        })
        .WithName("GetNetworkDisks")
        .WithSummary("获取网络磁盘列表");

        group.MapPost("/network/mount", async (MountNetworkDiskRequest request, IDiskManagementService service) =>
        {
            try
            {
                var result = await service.MountNetworkDiskAsync(
                    request.Server,
                    request.SharePath,
                    request.MountPoint,
                    request.DiskType,
                    request.Username,
                    request.Password,
                    request.Domain,
                    request.Options);
                return Results.Ok(new { message = "挂载网络磁盘成功", result });
            }
            catch (Exception ex)
            {
                return Results.Problem($"挂载网络磁盘失败: {ex.Message}");
            }
        })
        .WithName("MountNetworkDisk")
        .WithSummary("挂载网络磁盘");

        group.MapPost("/network/mount-permanent", async (MountNetworkDiskRequest request, IDiskManagementService service) =>
        {
            try
            {
                var result = await service.MountNetworkDiskPermanentAsync(
                    request.Server,
                    request.SharePath,
                    request.MountPoint,
                    request.DiskType,
                    request.Username,
                    request.Password,
                    request.Domain,
                    request.Options);
                return Results.Ok(new { message = "永久挂载网络磁盘成功", result });
            }
            catch (Exception ex)
            {
                return Results.Problem($"永久挂载网络磁盘失败: {ex.Message}");
            }
        })
        .WithName("MountNetworkDiskPermanent")
        .WithSummary("永久挂载网络磁盘");

        group.MapGet("/features", async (IDiskManagementService service) =>
        {
            try
            {
                var features = await service.DetectFeaturesAsync();
                return Results.Ok(features);
            }
            catch (Exception ex)
            {
                return Results.Problem($"检测功能失败: {ex.Message}");
            }
        })
        .WithName("DetectDiskFeatures")
        .WithSummary("检测磁盘管理功能");
    }

    // Request DTOs
    public record MountDiskRequest(string DevicePath, string MountPoint, string? FileSystem = null, string? Options = null);
    public record UnmountDiskRequest(string MountPoint);
    public record SetSpinDownRequest(int TimeoutMinutes);
    public record SetApmLevelRequest(int Level);
    public record MountNetworkDiskRequest(
        string Server, 
        string SharePath, 
        string MountPoint, 
        NetworkDiskType DiskType, 
        string? Username = null, 
        string? Password = null, 
        string? Domain = null, 
        string? Options = null);
}
