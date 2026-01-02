using QingFeng.Services;

namespace QingFeng.Endpoints;

public static class DockerEndpoints
{
    public static void MapDockerEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/docker")
            .WithTags("Docker");

        group.MapGet("/available", async (IDockerService service) =>
        {
            var available = await service.IsDockerAvailableAsync();
            return Results.Ok(new { available });
        })
        .WithName("GetDockerAvailability")
        .WithSummary("检查Docker是否可用");

        group.MapGet("/containers", async (bool? showAll, IDockerService service) =>
        {
            var containers = await service.GetContainersAsync(showAll ?? true);
            return Results.Ok(containers);
        })
        .WithName("GetContainers")
        .WithSummary("获取容器列表");

        group.MapGet("/images", async (IDockerService service) =>
        {
            var images = await service.GetImagesAsync();
            return Results.Ok(images);
        })
        .WithName("GetImages")
        .WithSummary("获取镜像列表");

        group.MapPost("/containers/{containerId}/start", async (string containerId, IDockerService service) =>
        {
            try
            {
                await service.StartContainerAsync(containerId);
                return Results.Ok(new { message = "容器已启动", containerId });
            }
            catch (Exception ex)
            {
                return Results.Problem($"启动容器失败: {ex.Message}");
            }
        })
        .WithName("StartContainer")
        .WithSummary("启动容器");

        group.MapPost("/containers/{containerId}/stop", async (string containerId, IDockerService service) =>
        {
            try
            {
                await service.StopContainerAsync(containerId);
                return Results.Ok(new { message = "容器已停止", containerId });
            }
            catch (Exception ex)
            {
                return Results.Problem($"停止容器失败: {ex.Message}");
            }
        })
        .WithName("StopContainer")
        .WithSummary("停止容器");

        group.MapPost("/containers/{containerId}/restart", async (string containerId, IDockerService service) =>
        {
            try
            {
                await service.RestartContainerAsync(containerId);
                return Results.Ok(new { message = "容器已重启", containerId });
            }
            catch (Exception ex)
            {
                return Results.Problem($"重启容器失败: {ex.Message}");
            }
        })
        .WithName("RestartContainer")
        .WithSummary("重启容器");

        group.MapDelete("/containers/{containerId}", async (string containerId, IDockerService service) =>
        {
            try
            {
                await service.RemoveContainerAsync(containerId);
                return Results.Ok(new { message = "容器已删除", containerId });
            }
            catch (Exception ex)
            {
                return Results.Problem($"删除容器失败: {ex.Message}");
            }
        })
        .WithName("RemoveContainer")
        .WithSummary("删除容器");

        group.MapGet("/containers/{containerId}/logs", async (string containerId, int? tailLines, IDockerService service) =>
        {
            try
            {
                var logs = await service.GetContainerLogsAsync(containerId, tailLines ?? 1000);
                return Results.Ok(new { logs });
            }
            catch (Exception ex)
            {
                return Results.Problem($"获取容器日志失败: {ex.Message}");
            }
        })
        .WithName("GetContainerLogs")
        .WithSummary("获取容器日志");
    }
}
