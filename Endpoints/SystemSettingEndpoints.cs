using QingFeng.Services;
using QingFeng.Models;

namespace QingFeng.Endpoints;

public static class SystemSettingEndpoints
{
    public static void MapSystemSettingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/settings")
            .WithTags("System Settings");

        group.MapGet("", async (ISystemSettingService service) =>
        {
            try
            {
                var settings = await service.GetAllSettingsAsync();
                return Results.Ok(settings);
            }
            catch (Exception ex)
            {
                return Results.Problem($"获取设置列表失败: {ex.Message}");
            }
        })
        .WithName("GetAllSettings")
        .WithSummary("获取所有系统设置");

        group.MapGet("/{key}", async (string key, ISystemSettingService service) =>
        {
            try
            {
                var value = await service.GetSettingAsync(key);
                if (value == null)
                {
                    return Results.NotFound();
                }
                return Results.Ok(new { key, value });
            }
            catch (Exception ex)
            {
                return Results.Problem($"获取设置失败: {ex.Message}");
            }
        })
        .WithName("GetSetting")
        .WithSummary("获取设置值");

        group.MapGet("/category/{category}", async (string category, ISystemSettingService service) =>
        {
            try
            {
                var settings = await service.GetSettingsByCategoryAsync(category);
                return Results.Ok(settings);
            }
            catch (Exception ex)
            {
                return Results.Problem($"获取分类设置失败: {ex.Message}");
            }
        })
        .WithName("GetSettingsByCategory")
        .WithSummary("按分类获取设置");

        group.MapPost("", async (SetSettingRequest request, ISystemSettingService service) =>
        {
            try
            {
                await service.SetSettingAsync(
                    request.Key, 
                    request.Value, 
                    request.Category ?? "", 
                    request.Description ?? "");
                return Results.Ok(new { message = "设置已保存", key = request.Key });
            }
            catch (Exception ex)
            {
                return Results.Problem($"保存设置失败: {ex.Message}");
            }
        })
        .WithName("SetSetting")
        .WithSummary("设置值");
    }

    // Request DTO
    public record SetSettingRequest(string Key, string Value, string? Category = "", string? Description = "");
}
