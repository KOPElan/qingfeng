using QingFeng.Services;
using QingFeng.Models;

namespace QingFeng.Endpoints;

public static class DockItemEndpoints
{
    public static void MapDockItemEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/dock")
            .WithTags("Dock Items");

        group.MapGet("", async (IDockItemService service) =>
        {
            try
            {
                var items = await service.GetAllDockItemsAsync();
                return Results.Ok(items);
            }
            catch (Exception ex)
            {
                return Results.Problem($"获取Dock项列表失败: {ex.Message}");
            }
        })
        .WithName("GetDockItems")
        .WithSummary("获取所有Dock项");

        group.MapGet("/{itemId}", async (string itemId, IDockItemService service) =>
        {
            try
            {
                var item = await service.GetDockItemAsync(itemId);
                if (item == null)
                {
                    return Results.NotFound();
                }
                return Results.Ok(item);
            }
            catch (Exception ex)
            {
                return Results.Problem($"获取Dock项失败: {ex.Message}");
            }
        })
        .WithName("GetDockItem")
        .WithSummary("获取Dock项详情");

        group.MapPost("", async (DockItem item, IDockItemService service) =>
        {
            try
            {
                var savedItem = await service.SaveDockItemAsync(item);
                return Results.Ok(savedItem);
            }
            catch (Exception ex)
            {
                return Results.Problem($"保存Dock项失败: {ex.Message}");
            }
        })
        .WithName("SaveDockItem")
        .WithSummary("保存Dock项");

        group.MapDelete("/{itemId}", async (string itemId, IDockItemService service) =>
        {
            try
            {
                var result = await service.DeleteDockItemAsync(itemId);
                if (!result)
                {
                    return Results.NotFound();
                }
                return Results.Ok(new { message = "Dock项已删除", itemId });
            }
            catch (Exception ex)
            {
                return Results.Problem($"删除Dock项失败: {ex.Message}");
            }
        })
        .WithName("DeleteDockItem")
        .WithSummary("删除Dock项");
    }
}
