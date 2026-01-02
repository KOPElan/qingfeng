using QingFeng.Services;
using QingFeng.Models;

namespace QingFeng.Endpoints;

public static class ApplicationEndpoints
{
    public static void MapApplicationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/applications")
            .WithTags("Applications");

        group.MapGet("", async (IApplicationService service) =>
        {
            try
            {
                var applications = await service.GetAllApplicationsAsync();
                return Results.Ok(applications);
            }
            catch (Exception ex)
            {
                return Results.Problem($"获取应用列表失败: {ex.Message}");
            }
        })
        .WithName("GetApplications")
        .WithSummary("获取所有应用");

        group.MapGet("/{appId}", async (string appId, IApplicationService service) =>
        {
            try
            {
                var application = await service.GetApplicationAsync(appId);
                if (application == null)
                {
                    return Results.NotFound();
                }
                return Results.Ok(application);
            }
            catch (Exception ex)
            {
                return Results.Problem($"获取应用失败: {ex.Message}");
            }
        })
        .WithName("GetApplication")
        .WithSummary("获取应用详情");

        group.MapPost("", async (Application application, IApplicationService service) =>
        {
            try
            {
                var savedApp = await service.SaveApplicationAsync(application);
                return Results.Ok(savedApp);
            }
            catch (Exception ex)
            {
                return Results.Problem($"保存应用失败: {ex.Message}");
            }
        })
        .WithName("SaveApplication")
        .WithSummary("保存应用");

        group.MapDelete("/{appId}", async (string appId, IApplicationService service) =>
        {
            try
            {
                var result = await service.DeleteApplicationAsync(appId);
                if (!result)
                {
                    return Results.NotFound();
                }
                return Results.Ok(new { message = "应用已删除", appId });
            }
            catch (Exception ex)
            {
                return Results.Problem($"删除应用失败: {ex.Message}");
            }
        })
        .WithName("DeleteApplication")
        .WithSummary("删除应用");

        group.MapPost("/{appId}/toggle-pin", async (string appId, IApplicationService service) =>
        {
            try
            {
                var result = await service.TogglePinToDockAsync(appId);
                if (!result)
                {
                    return Results.NotFound();
                }
                return Results.Ok(new { message = "固定状态已切换", appId });
            }
            catch (Exception ex)
            {
                return Results.Problem($"切换固定状态失败: {ex.Message}");
            }
        })
        .WithName("ToggleApplicationPin")
        .WithSummary("切换应用固定到Dock");
    }
}
