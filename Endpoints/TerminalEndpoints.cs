using QingFeng.Services;

namespace QingFeng.Endpoints;

public static class TerminalEndpoints
{
    public static void MapTerminalEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/terminal")
            .WithTags("Terminal");

        group.MapPost("/sessions", async (ITerminalService service) =>
        {
            try
            {
                var sessionId = await service.CreateSessionAsync();
                return Results.Ok(new { sessionId, message = "终端会话已创建" });
            }
            catch (Exception ex)
            {
                return Results.Problem($"创建终端会话失败: {ex.Message}");
            }
        })
        .WithName("CreateTerminalSession")
        .WithSummary("创建终端会话");

        group.MapPost("/sessions/{sessionId}/input", async (string sessionId, WriteInputRequest request, ITerminalService service) =>
        {
            try
            {
                if (!service.SessionExists(sessionId))
                {
                    return Results.NotFound(new { message = "会话不存在" });
                }
                await service.WriteInputAsync(sessionId, request.Input);
                return Results.Ok(new { message = "输入已发送" });
            }
            catch (Exception ex)
            {
                return Results.Problem($"写入输入失败: {ex.Message}");
            }
        })
        .WithName("WriteTerminalInput")
        .WithSummary("写入终端输入");

        group.MapGet("/sessions/{sessionId}/output", async (string sessionId, ITerminalService service) =>
        {
            try
            {
                if (!service.SessionExists(sessionId))
                {
                    return Results.NotFound(new { message = "会话不存在" });
                }
                var output = await service.ReadOutputAsync(sessionId);
                return Results.Ok(new { output });
            }
            catch (Exception ex)
            {
                return Results.Problem($"读取输出失败: {ex.Message}");
            }
        })
        .WithName("ReadTerminalOutput")
        .WithSummary("读取终端输出");

        group.MapPost("/sessions/{sessionId}/resize", async (string sessionId, ResizeRequest request, ITerminalService service) =>
        {
            try
            {
                if (!service.SessionExists(sessionId))
                {
                    return Results.NotFound(new { message = "会话不存在" });
                }
                await service.ResizeTerminalAsync(sessionId, request.Rows, request.Cols);
                return Results.Ok(new { message = "终端已调整大小" });
            }
            catch (Exception ex)
            {
                return Results.Problem($"调整终端大小失败: {ex.Message}");
            }
        })
        .WithName("ResizeTerminal")
        .WithSummary("调整终端大小");

        group.MapDelete("/sessions/{sessionId}", async (string sessionId, ITerminalService service) =>
        {
            try
            {
                if (!service.SessionExists(sessionId))
                {
                    return Results.NotFound(new { message = "会话不存在" });
                }
                await service.CloseSessionAsync(sessionId);
                return Results.Ok(new { message = "终端会话已关闭" });
            }
            catch (Exception ex)
            {
                return Results.Problem($"关闭终端会话失败: {ex.Message}");
            }
        })
        .WithName("CloseTerminalSession")
        .WithSummary("关闭终端会话");
    }

    // Request DTOs
    public record WriteInputRequest(string Input);
    public record ResizeRequest(int Rows, int Cols);
}
