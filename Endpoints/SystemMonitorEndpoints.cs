using QingFeng.Services;

namespace QingFeng.Endpoints;

public static class SystemMonitorEndpoints
{
    public static void MapSystemMonitorEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/system")
            .WithTags("System Monitor");

        group.MapGet("/resources", async (ISystemMonitorService service) =>
        {
            var info = await service.GetSystemResourceInfoAsync();
            return Results.Ok(info);
        })
        .WithName("GetSystemResources")
        .WithSummary("获取系统资源信息");

        group.MapGet("/cpu", async (ISystemMonitorService service) =>
        {
            var info = await service.GetCpuInfoAsync();
            return Results.Ok(info);
        })
        .WithName("GetCpuInfo")
        .WithSummary("获取CPU信息");

        group.MapGet("/memory", async (ISystemMonitorService service) =>
        {
            var info = await service.GetMemoryInfoAsync();
            return Results.Ok(info);
        })
        .WithName("GetMemoryInfo")
        .WithSummary("获取内存信息");

        group.MapGet("/disks", async (ISystemMonitorService service) =>
        {
            var info = await service.GetDiskInfoAsync();
            return Results.Ok(info);
        })
        .WithName("GetDiskInfo")
        .WithSummary("获取磁盘信息");

        group.MapGet("/network", async (ISystemMonitorService service) =>
        {
            var info = await service.GetNetworkInfoAsync();
            return Results.Ok(info);
        })
        .WithName("GetNetworkInfo")
        .WithSummary("获取网络信息");
    }
}
