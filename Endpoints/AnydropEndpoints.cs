using QingFeng.Services;

namespace QingFeng.Endpoints;

public static class AnydropEndpoints
{
    public static void MapAnydropEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/anydrop")
            .WithTags("Anydrop");

        group.MapGet("/messages", async (int? pageSize, int? beforeMessageId, IAnydropService service) =>
        {
            try
            {
                var messages = await service.GetMessagesAsync(pageSize ?? 20, beforeMessageId);
                return Results.Ok(messages);
            }
            catch (Exception ex)
            {
                return Results.Problem($"获取消息列表失败: {ex.Message}");
            }
        })
        .WithName("GetAnydropMessages")
        .WithSummary("获取消息列表");

        group.MapGet("/messages/{messageId}", async (int messageId, IAnydropService service) =>
        {
            try
            {
                var message = await service.GetMessageByIdAsync(messageId);
                if (message == null)
                {
                    return Results.NotFound();
                }
                return Results.Ok(message);
            }
            catch (Exception ex)
            {
                return Results.Problem($"获取消息失败: {ex.Message}");
            }
        })
        .WithName("GetAnydropMessageById")
        .WithSummary("获取消息详情");

        group.MapPost("/messages", async (CreateMessageRequest request, IAnydropService service) =>
        {
            try
            {
                var message = await service.CreateMessageAsync(request.Content, request.MessageType ?? "Text");
                return Results.Ok(message);
            }
            catch (Exception ex)
            {
                return Results.Problem($"创建消息失败: {ex.Message}");
            }
        })
        .WithName("CreateAnydropMessage")
        .WithSummary("创建消息");

        group.MapPost("/messages/{messageId}/attachments", async (
            int messageId, 
            HttpRequest request, 
            IAnydropService service,
            ILogger<Program> logger) =>
        {
            try
            {
                if (!request.HasFormContentType)
                {
                    return Results.BadRequest("Invalid content type. Expected multipart/form-data.");
                }

                var form = await request.ReadFormAsync();
                if (form.Files.Count == 0)
                {
                    return Results.BadRequest("No file uploaded.");
                }

                // Note: Currently processes only the first file
                // TODO: Add file type validation and size limits for security
                var file = form.Files[0];
                var contentType = file.ContentType ?? "application/octet-stream";

                using var stream = file.OpenReadStream();
                var attachment = await service.AddAttachmentAsync(messageId, file.FileName, stream, file.Length, contentType);
                
                return Results.Ok(attachment);
            }
            catch (Exception ex)
            {
                return Results.Problem($"添加附件失败: {ex.Message}");
            }
        })
        .DisableAntiforgery()
        .WithName("AddAnydropAttachment")
        .WithSummary("添加消息附件");

        group.MapGet("/messages/search", async (string searchTerm, IAnydropService service) =>
        {
            try
            {
                var messages = await service.SearchMessagesAsync(searchTerm);
                return Results.Ok(messages);
            }
            catch (Exception ex)
            {
                return Results.Problem($"搜索消息失败: {ex.Message}");
            }
        })
        .WithName("SearchAnydropMessages")
        .WithSummary("搜索消息");

        group.MapDelete("/messages/{messageId}", async (int messageId, IAnydropService service) =>
        {
            try
            {
                await service.DeleteMessageAsync(messageId);
                return Results.Ok(new { message = "消息已删除", messageId });
            }
            catch (Exception ex)
            {
                return Results.Problem($"删除消息失败: {ex.Message}");
            }
        })
        .WithName("DeleteAnydropMessage")
        .WithSummary("删除消息");

        group.MapGet("/messages/count", async (IAnydropService service) =>
        {
            try
            {
                var count = await service.GetTotalMessageCountAsync();
                return Results.Ok(new { count });
            }
            catch (Exception ex)
            {
                return Results.Problem($"获取消息数量失败: {ex.Message}");
            }
        })
        .WithName("GetAnydropMessageCount")
        .WithSummary("获取消息总数");

        // Attachment download endpoints (kept in Program.cs for backward compatibility)
        // These are also mapped in Program.cs to maintain existing API routes
    }

    // Request DTO
    public record CreateMessageRequest(string Content, string? MessageType = "Text");
}
