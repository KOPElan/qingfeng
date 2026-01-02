using QingFeng.Services;

namespace QingFeng.Endpoints;

public static class AuthenticationEndpoints
{
    public static void MapAuthenticationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth")
            .WithTags("Authentication");

        group.MapPost("/login", async (LoginRequest request, IAuthenticationService service) =>
        {
            try
            {
                var user = await service.LoginAsync(request.Username, request.Password);
                if (user == null)
                {
                    return Results.Unauthorized();
                }
                return Results.Ok(new 
                { 
                    message = "登录成功",
                    user = new 
                    {
                        user.Id,
                        user.Username,
                        user.Role
                    }
                });
            }
            catch (Exception ex)
            {
                return Results.Problem($"登录失败: {ex.Message}");
            }
        })
        .WithName("Login")
        .WithSummary("用户登录");

        group.MapPost("/logout", async (IAuthenticationService service) =>
        {
            try
            {
                await service.LogoutAsync();
                return Results.Ok(new { message = "登出成功" });
            }
            catch (Exception ex)
            {
                return Results.Problem($"登出失败: {ex.Message}");
            }
        })
        .WithName("Logout")
        .WithSummary("用户登出");

        group.MapGet("/current", async (IAuthenticationService service) =>
        {
            try
            {
                var user = await service.GetCurrentUserAsync();
                if (user == null)
                {
                    return Results.Unauthorized();
                }
                return Results.Ok(new 
                {
                    user.Id,
                    user.Username,
                    user.Role
                });
            }
            catch (Exception ex)
            {
                return Results.Problem($"获取当前用户失败: {ex.Message}");
            }
        })
        .WithName("GetCurrentUser")
        .WithSummary("获取当前用户");

        group.MapGet("/users", async (IAuthenticationService service) =>
        {
            try
            {
                var isAdmin = await service.IsAdminAsync();
                if (!isAdmin)
                {
                    return Results.Forbid();
                }
                var users = await service.GetAllUsersAsync();
                return Results.Ok(users.Select(u => new 
                {
                    u.Id,
                    u.Username,
                    u.Role
                }));
            }
            catch (Exception ex)
            {
                return Results.Problem($"获取用户列表失败: {ex.Message}");
            }
        })
        .WithName("GetAllUsers")
        .WithSummary("获取所有用户");

        group.MapPost("/users", async (CreateUserRequest request, IAuthenticationService service) =>
        {
            try
            {
                var isAdmin = await service.IsAdminAsync();
                if (!isAdmin)
                {
                    return Results.Forbid();
                }
                var user = await service.CreateUserAsync(request.Username, request.Password, request.Role);
                return Results.Ok(new 
                { 
                    message = "用户已创建",
                    user = new 
                    {
                        user.Id,
                        user.Username,
                        user.Role
                    }
                });
            }
            catch (Exception ex)
            {
                return Results.Problem($"创建用户失败: {ex.Message}");
            }
        })
        .WithName("CreateUser")
        .WithSummary("创建用户");

        group.MapDelete("/users/{userId}", async (int userId, IAuthenticationService service) =>
        {
            try
            {
                var isAdmin = await service.IsAdminAsync();
                if (!isAdmin)
                {
                    return Results.Forbid();
                }
                await service.DeleteUserAsync(userId);
                return Results.Ok(new { message = "用户已删除", userId });
            }
            catch (Exception ex)
            {
                return Results.Problem($"删除用户失败: {ex.Message}");
            }
        })
        .WithName("DeleteUser")
        .WithSummary("删除用户");
    }

    // Request DTOs
    public record LoginRequest(string Username, string Password);
    public record CreateUserRequest(string Username, string Password, string Role);
}
