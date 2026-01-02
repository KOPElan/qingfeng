using QingFeng.Services;
using QingFeng.Models;

namespace QingFeng.Endpoints;

public static class ScheduledTaskEndpoints
{
    public static void MapScheduledTaskEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/tasks")
            .WithTags("Scheduled Tasks");

        group.MapGet("", async (IScheduledTaskService service) =>
        {
            try
            {
                var tasks = await service.GetAllTasksAsync();
                return Results.Ok(tasks);
            }
            catch (Exception ex)
            {
                return Results.Problem($"获取任务列表失败: {ex.Message}");
            }
        })
        .WithName("GetScheduledTasks")
        .WithSummary("获取所有计划任务");

        group.MapGet("/{id}", async (int id, IScheduledTaskService service) =>
        {
            try
            {
                var task = await service.GetTaskAsync(id);
                if (task == null)
                {
                    return Results.NotFound();
                }
                return Results.Ok(task);
            }
            catch (Exception ex)
            {
                return Results.Problem($"获取任务失败: {ex.Message}");
            }
        })
        .WithName("GetScheduledTask")
        .WithSummary("获取计划任务详情");

        group.MapPost("", async (ScheduledTask task, IScheduledTaskService service) =>
        {
            try
            {
                var createdTask = await service.CreateTaskAsync(task);
                return Results.Ok(createdTask);
            }
            catch (Exception ex)
            {
                return Results.Problem($"创建任务失败: {ex.Message}");
            }
        })
        .WithName("CreateScheduledTask")
        .WithSummary("创建计划任务");

        group.MapPut("/{id}", async (int id, ScheduledTask task, IScheduledTaskService service) =>
        {
            try
            {
                task.Id = id;
                await service.UpdateTaskAsync(task);
                return Results.Ok(new { message = "任务已更新", id });
            }
            catch (Exception ex)
            {
                return Results.Problem($"更新任务失败: {ex.Message}");
            }
        })
        .WithName("UpdateScheduledTask")
        .WithSummary("更新计划任务");

        group.MapDelete("/{id}", async (int id, IScheduledTaskService service) =>
        {
            try
            {
                await service.DeleteTaskAsync(id);
                return Results.Ok(new { message = "任务已删除", id });
            }
            catch (Exception ex)
            {
                return Results.Problem($"删除任务失败: {ex.Message}");
            }
        })
        .WithName("DeleteScheduledTask")
        .WithSummary("删除计划任务");

        group.MapPost("/{id}/enable", async (int id, EnableTaskRequest request, IScheduledTaskService service) =>
        {
            try
            {
                await service.SetTaskEnabledAsync(id, request.Enabled);
                return Results.Ok(new { message = request.Enabled ? "任务已启用" : "任务已禁用", id });
            }
            catch (Exception ex)
            {
                return Results.Problem($"设置任务状态失败: {ex.Message}");
            }
        })
        .WithName("EnableScheduledTask")
        .WithSummary("启用/禁用计划任务");

        group.MapPost("/{id}/run", async (int id, IScheduledTaskService service) =>
        {
            try
            {
                await service.RunTaskNowAsync(id);
                return Results.Ok(new { message = "任务已开始运行", id });
            }
            catch (Exception ex)
            {
                return Results.Problem($"运行任务失败: {ex.Message}");
            }
        })
        .WithName("RunScheduledTask")
        .WithSummary("立即运行计划任务");
    }

    // Request DTO
    public record EnableTaskRequest(bool Enabled);
}
