using QingFeng.Services;
using QingFeng.Models;

namespace QingFeng.Endpoints;

public static class FileManagerEndpoints
{
    public static void MapFileManagerEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/files")
            .WithTags("File Manager");

        group.MapGet("", async (string path, IFileManagerService service) =>
        {
            try
            {
                // Note: Path validation is handled by IFileManagerService.IsPathAllowed()
                var files = await service.GetFilesAsync(path);
                return Results.Ok(files);
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
            catch (Exception ex)
            {
                return Results.Problem($"获取文件列表失败: {ex.Message}");
            }
        })
        .WithName("GetFiles")
        .WithSummary("获取文件列表");

        group.MapGet("/drives", async (IFileManagerService service) =>
        {
            try
            {
                var drives = await service.GetDrivesAsync();
                return Results.Ok(drives);
            }
            catch (Exception ex)
            {
                return Results.Problem($"获取驱动器列表失败: {ex.Message}");
            }
        })
        .WithName("GetDrives")
        .WithSummary("获取驱动器列表");

        group.MapGet("/storage-info", async (string path, IFileManagerService service) =>
        {
            try
            {
                var (total, available) = await service.GetStorageInfoAsync(path);
                return Results.Ok(new { total, available, used = total - available });
            }
            catch (Exception ex)
            {
                return Results.Problem($"获取存储信息失败: {ex.Message}");
            }
        })
        .WithName("GetStorageInfo")
        .WithSummary("获取存储信息");

        group.MapPost("/directory", async (CreateDirectoryRequest request, IFileManagerService service) =>
        {
            try
            {
                await service.CreateDirectoryAsync(request.Path);
                return Results.Ok(new { message = "目录已创建", path = request.Path });
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
            catch (Exception ex)
            {
                return Results.Problem($"创建目录失败: {ex.Message}");
            }
        })
        .WithName("CreateDirectory")
        .WithSummary("创建目录");

        group.MapDelete("", async (string path, bool isDirectory, IFileManagerService service) =>
        {
            try
            {
                if (isDirectory)
                {
                    await service.DeleteDirectoryAsync(path);
                }
                else
                {
                    await service.DeleteFileAsync(path);
                }
                return Results.Ok(new { message = "删除成功", path });
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
            catch (Exception ex)
            {
                return Results.Problem($"删除失败: {ex.Message}");
            }
        })
        .WithName("DeleteFile")
        .WithSummary("删除文件或目录");

        group.MapPost("/rename", async (RenameRequest request, IFileManagerService service) =>
        {
            try
            {
                await service.RenameAsync(request.OldPath, request.NewPath);
                return Results.Ok(new { message = "重命名成功", oldPath = request.OldPath, newPath = request.NewPath });
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
            catch (Exception ex)
            {
                return Results.Problem($"重命名失败: {ex.Message}");
            }
        })
        .WithName("RenameFile")
        .WithSummary("重命名文件或目录");

        group.MapPost("/copy", async (CopyMoveRequest request, IFileManagerService service) =>
        {
            try
            {
                await service.CopyAsync(request.SourcePath, request.DestinationPath);
                return Results.Ok(new { message = "复制成功", sourcePath = request.SourcePath, destinationPath = request.DestinationPath });
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
            catch (Exception ex)
            {
                return Results.Problem($"复制失败: {ex.Message}");
            }
        })
        .WithName("CopyFile")
        .WithSummary("复制文件或目录");

        group.MapPost("/move", async (CopyMoveRequest request, IFileManagerService service) =>
        {
            try
            {
                await service.MoveAsync(request.SourcePath, request.DestinationPath);
                return Results.Ok(new { message = "移动成功", sourcePath = request.SourcePath, destinationPath = request.DestinationPath });
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
            catch (Exception ex)
            {
                return Results.Problem($"移动失败: {ex.Message}");
            }
        })
        .WithName("MoveFile")
        .WithSummary("移动文件或目录");

        group.MapPost("/search", async (SearchRequest request, IFileManagerService service) =>
        {
            try
            {
                var results = await service.SearchFilesAsync(
                    request.Path, 
                    request.SearchPattern, 
                    request.MaxResults ?? 1000,
                    request.MaxDepth ?? 10);
                return Results.Ok(results);
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
            catch (Exception ex)
            {
                return Results.Problem($"搜索失败: {ex.Message}");
            }
        })
        .WithName("SearchFiles")
        .WithSummary("搜索文件");

        group.MapPost("/batch/copy", async (BatchOperationRequest request, IFileManagerService service) =>
        {
            try
            {
                await service.BatchCopyAsync(request.SourcePaths, request.DestinationPath);
                return Results.Ok(new { message = "批量复制成功", count = request.SourcePaths.Count });
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
            catch (Exception ex)
            {
                return Results.Problem($"批量复制失败: {ex.Message}");
            }
        })
        .WithName("BatchCopy")
        .WithSummary("批量复制");

        group.MapPost("/batch/move", async (BatchOperationRequest request, IFileManagerService service) =>
        {
            try
            {
                await service.BatchMoveAsync(request.SourcePaths, request.DestinationPath);
                return Results.Ok(new { message = "批量移动成功", count = request.SourcePaths.Count });
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
            catch (Exception ex)
            {
                return Results.Problem($"批量移动失败: {ex.Message}");
            }
        })
        .WithName("BatchMove")
        .WithSummary("批量移动");

        group.MapPost("/batch/delete", async (BatchDeleteRequest request, IFileManagerService service) =>
        {
            try
            {
                await service.BatchDeleteAsync(request.Paths);
                return Results.Ok(new { message = "批量删除成功", count = request.Paths.Count });
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
            catch (Exception ex)
            {
                return Results.Problem($"批量删除失败: {ex.Message}");
            }
        })
        .WithName("BatchDelete")
        .WithSummary("批量删除");

        // Favorites endpoints
        group.MapGet("/favorites", async (IFileManagerService service) =>
        {
            try
            {
                var favorites = await service.GetFavoriteFoldersAsync();
                return Results.Ok(favorites);
            }
            catch (Exception ex)
            {
                return Results.Problem($"获取收藏夹失败: {ex.Message}");
            }
        })
        .WithName("GetFavorites")
        .WithSummary("获取收藏夹");

        group.MapPost("/favorites", async (AddFavoriteRequest request, IFileManagerService service) =>
        {
            try
            {
                var favorite = await service.AddFavoriteFolderAsync(request.Name, request.Path, request.Icon ?? "folder");
                return Results.Ok(favorite);
            }
            catch (Exception ex)
            {
                return Results.Problem($"添加收藏夹失败: {ex.Message}");
            }
        })
        .WithName("AddFavorite")
        .WithSummary("添加收藏夹");

        group.MapPut("/favorites/{id}", async (int id, UpdateFavoriteRequest request, IFileManagerService service) =>
        {
            try
            {
                await service.UpdateFavoriteFolderAsync(id, request.Name, request.Icon);
                return Results.Ok(new { message = "收藏夹已更新", id });
            }
            catch (Exception ex)
            {
                return Results.Problem($"更新收藏夹失败: {ex.Message}");
            }
        })
        .WithName("UpdateFavorite")
        .WithSummary("更新收藏夹");

        group.MapDelete("/favorites/{id}", async (int id, IFileManagerService service) =>
        {
            try
            {
                await service.RemoveFavoriteFolderAsync(id);
                return Results.Ok(new { message = "收藏夹已删除", id });
            }
            catch (Exception ex)
            {
                return Results.Problem($"删除收藏夹失败: {ex.Message}");
            }
        })
        .WithName("DeleteFavorite")
        .WithSummary("删除收藏夹");
    }

    // Request DTOs
    public record CreateDirectoryRequest(string Path);
    public record RenameRequest(string OldPath, string NewPath);
    public record CopyMoveRequest(string SourcePath, string DestinationPath);
    public record SearchRequest(string Path, string SearchPattern, int? MaxResults = 1000, int? MaxDepth = 10);
    public record BatchOperationRequest(List<string> SourcePaths, string DestinationPath);
    public record BatchDeleteRequest(List<string> Paths);
    public record AddFavoriteRequest(string Name, string Path, string? Icon = "folder");
    public record UpdateFavoriteRequest(string Name, string Icon);
}
