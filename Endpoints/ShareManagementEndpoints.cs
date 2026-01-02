using QingFeng.Services;
using QingFeng.Models;

namespace QingFeng.Endpoints;

public static class ShareManagementEndpoints
{
    public static void MapShareManagementEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/shares")
            .WithTags("Share Management");

        group.MapGet("", async (IShareManagementService service) =>
        {
            try
            {
                var shares = await service.GetAllSharesAsync();
                return Results.Ok(shares);
            }
            catch (Exception ex)
            {
                return Results.Problem($"获取共享列表失败: {ex.Message}");
            }
        })
        .WithName("GetAllShares")
        .WithSummary("获取所有共享");

        group.MapGet("/cifs", async (IShareManagementService service) =>
        {
            try
            {
                var shares = await service.GetCifsSharesAsync();
                return Results.Ok(shares);
            }
            catch (Exception ex)
            {
                return Results.Problem($"获取CIFS共享列表失败: {ex.Message}");
            }
        })
        .WithName("GetCifsShares")
        .WithSummary("获取CIFS共享列表");

        group.MapGet("/nfs", async (IShareManagementService service) =>
        {
            try
            {
                var shares = await service.GetNfsSharesAsync();
                return Results.Ok(shares);
            }
            catch (Exception ex)
            {
                return Results.Problem($"获取NFS共享列表失败: {ex.Message}");
            }
        })
        .WithName("GetNfsShares")
        .WithSummary("获取NFS共享列表");

        group.MapPost("/cifs", async (ShareRequest request, IShareManagementService service) =>
        {
            try
            {
                var result = await service.AddCifsShareAsync(request);
                return Results.Ok(new { message = "CIFS共享已创建", result });
            }
            catch (Exception ex)
            {
                return Results.Problem($"创建CIFS共享失败: {ex.Message}");
            }
        })
        .WithName("AddCifsShare")
        .WithSummary("添加CIFS共享");

        group.MapPost("/nfs", async (ShareRequest request, IShareManagementService service) =>
        {
            try
            {
                var result = await service.AddNfsShareAsync(request);
                return Results.Ok(new { message = "NFS共享已创建", result });
            }
            catch (Exception ex)
            {
                return Results.Problem($"创建NFS共享失败: {ex.Message}");
            }
        })
        .WithName("AddNfsShare")
        .WithSummary("添加NFS共享");

        group.MapPut("/cifs/{shareName}", async (string shareName, ShareRequest request, IShareManagementService service) =>
        {
            try
            {
                var result = await service.UpdateCifsShareAsync(shareName, request);
                return Results.Ok(new { message = "CIFS共享已更新", result });
            }
            catch (Exception ex)
            {
                return Results.Problem($"更新CIFS共享失败: {ex.Message}");
            }
        })
        .WithName("UpdateCifsShare")
        .WithSummary("更新CIFS共享");

        group.MapPut("/nfs/{exportPath}", async (string exportPath, ShareRequest request, IShareManagementService service) =>
        {
            try
            {
                var result = await service.UpdateNfsShareAsync(exportPath, request);
                return Results.Ok(new { message = "NFS共享已更新", result });
            }
            catch (Exception ex)
            {
                return Results.Problem($"更新NFS共享失败: {ex.Message}");
            }
        })
        .WithName("UpdateNfsShare")
        .WithSummary("更新NFS共享");

        group.MapDelete("/cifs/{shareName}", async (string shareName, IShareManagementService service) =>
        {
            try
            {
                var result = await service.RemoveCifsShareAsync(shareName);
                return Results.Ok(new { message = "CIFS共享已删除", result });
            }
            catch (Exception ex)
            {
                return Results.Problem($"删除CIFS共享失败: {ex.Message}");
            }
        })
        .WithName("RemoveCifsShare")
        .WithSummary("删除CIFS共享");

        group.MapDelete("/nfs/{exportPath}", async (string exportPath, IShareManagementService service) =>
        {
            try
            {
                var result = await service.RemoveNfsShareAsync(exportPath);
                return Results.Ok(new { message = "NFS共享已删除", result });
            }
            catch (Exception ex)
            {
                return Results.Problem($"删除NFS共享失败: {ex.Message}");
            }
        })
        .WithName("RemoveNfsShare")
        .WithSummary("删除NFS共享");

        group.MapPost("/cifs/restart", async (IShareManagementService service) =>
        {
            try
            {
                var result = await service.RestartSambaServiceAsync();
                return Results.Ok(new { message = "Samba服务已重启", result });
            }
            catch (Exception ex)
            {
                return Results.Problem($"重启Samba服务失败: {ex.Message}");
            }
        })
        .WithName("RestartSambaService")
        .WithSummary("重启Samba服务");

        group.MapPost("/nfs/restart", async (IShareManagementService service) =>
        {
            try
            {
                var result = await service.RestartNfsServiceAsync();
                return Results.Ok(new { message = "NFS服务已重启", result });
            }
            catch (Exception ex)
            {
                return Results.Problem($"重启NFS服务失败: {ex.Message}");
            }
        })
        .WithName("RestartNfsService")
        .WithSummary("重启NFS服务");

        group.MapPost("/cifs/test-config", async (IShareManagementService service) =>
        {
            try
            {
                var result = await service.TestSambaConfigAsync();
                return Results.Ok(new { message = "配置测试完成", result });
            }
            catch (Exception ex)
            {
                return Results.Problem($"测试Samba配置失败: {ex.Message}");
            }
        })
        .WithName("TestSambaConfig")
        .WithSummary("测试Samba配置");

        group.MapGet("/features", async (IShareManagementService service) =>
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
        .WithName("DetectShareFeatures")
        .WithSummary("检测共享管理功能");

        // Samba user management
        group.MapGet("/samba-users", async (IShareManagementService service) =>
        {
            try
            {
                var users = await service.GetSambaUsersAsync();
                return Results.Ok(users);
            }
            catch (Exception ex)
            {
                return Results.Problem($"获取Samba用户列表失败: {ex.Message}");
            }
        })
        .WithName("GetSambaUsers")
        .WithSummary("获取Samba用户列表");

        group.MapPost("/samba-users", async (SambaUserRequest request, IShareManagementService service) =>
        {
            try
            {
                var result = await service.AddSambaUserAsync(request);
                if (result.Success)
                {
                    return Results.Ok(new { message = "Samba用户已创建", username = request.Username });
                }
                return Results.Problem(result.Message);
            }
            catch (Exception ex)
            {
                return Results.Problem($"创建Samba用户失败: {ex.Message}");
            }
        })
        .WithName("AddSambaUser")
        .WithSummary("添加Samba用户");

        group.MapPut("/samba-users/{username}", async (string username, UpdateSambaUserRequest request, IShareManagementService service) =>
        {
            try
            {
                var result = await service.UpdateSambaUserPasswordAsync(username, request.Password);
                if (result.Success)
                {
                    return Results.Ok(new { message = "Samba用户密码已更新", username });
                }
                return Results.Problem(result.Message);
            }
            catch (Exception ex)
            {
                return Results.Problem($"更新Samba用户失败: {ex.Message}");
            }
        })
        .WithName("UpdateSambaUser")
        .WithSummary("更新Samba用户");

        group.MapDelete("/samba-users/{username}", async (string username, IShareManagementService service) =>
        {
            try
            {
                var result = await service.RemoveSambaUserAsync(username);
                if (result.Success)
                {
                    return Results.Ok(new { message = "Samba用户已删除", username });
                }
                return Results.Problem(result.Message);
            }
            catch (Exception ex)
            {
                return Results.Problem($"删除Samba用户失败: {ex.Message}");
            }
        })
        .WithName("RemoveSambaUser")
        .WithSummary("删除Samba用户");
    }

    // Request DTO
    public record UpdateSambaUserRequest(string Password);
}
