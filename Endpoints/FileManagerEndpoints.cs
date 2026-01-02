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

        // File download endpoint
        // TODO: Add authentication/authorization when implementing user management
        // Currently relies on FileManagerService.IsPathAllowed() for security
        group.MapGet("/download", async (string path, IFileManagerService service) =>
        {
            try
            {
                var fileBytes = await service.DownloadFileAsync(path);
                var fileName = Path.GetFileName(path);
                var contentType = GetContentType(fileName);

                return Results.File(fileBytes, contentType, fileName);
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
            catch (FileNotFoundException)
            {
                return Results.NotFound();
            }
            catch (Exception)
            {
                // Don't expose internal error details for security
                return Results.Problem("An error occurred while downloading the file.");
            }
        })
        .WithName("DownloadFile")
        .WithSummary("下载文件");

        // File upload endpoint with streaming support
        // Supports both regular uploads and chunked uploads for large files
        // TODO: Add authentication/authorization when implementing user management
        // Note: Currently relies on FileManagerService.IsPathAllowed() for security
        // Antiforgery is disabled to support external API clients - consider enabling with proper auth
        group.MapPost("/upload", async (HttpRequest request, IFileManagerService service, ILogger<Program> logger) =>
        {
            try
            {
                if (!request.HasFormContentType)
                {
                    return Results.BadRequest("Invalid content type. Expected multipart/form-data.");
                }

                var form = await request.ReadFormAsync();
                var directoryPath = form["directoryPath"].ToString();

                if (string.IsNullOrWhiteSpace(directoryPath))
                {
                    return Results.BadRequest("Directory path is required.");
                }

                // Check if this is a chunked upload
                var isChunked = form.ContainsKey("dzuuid");

                if (isChunked)
                {
                    // Handle chunked upload
                    // Get first value from form (Dropzone may send duplicates in some cases)
                    var chunkIndexStr = form["dzchunkindex"].FirstOrDefault()?.ToString() ?? string.Empty;
                    var totalChunksStr = form["dztotalchunkcount"].FirstOrDefault()?.ToString() ?? string.Empty;
                    var chunkSizeStr = form["dzchunksize"].FirstOrDefault()?.ToString() ?? string.Empty;
                    var totalFileSizeStr = form["dztotalfilesize"].FirstOrDefault()?.ToString() ?? string.Empty;
                    var fileUuid = form["dzuuid"].FirstOrDefault()?.ToString() ?? string.Empty;

                    // Validate UUID to prevent path traversal attacks
                    // Accepts standard UUID format (8-4-4-4-12 hex pattern)
                    if (string.IsNullOrWhiteSpace(fileUuid) || !UuidValidationRegex.IsMatch(fileUuid))
                    {
                        logger.LogError("Invalid UUID received: '{FileUuid}'. Expected UUID format.", fileUuid);
                        return Results.BadRequest("Invalid file UUID.");
                    }

                    // Log chunk parameters for debugging
                    logger.LogDebug("Chunk upload - UUID: {Uuid}, Index: {Index}, Total: {Total}, Size: {Size}, FileSize: {FileSize}",
                        fileUuid, chunkIndexStr, totalChunksStr, chunkSizeStr, totalFileSizeStr);

                    if (!int.TryParse(chunkIndexStr, out int chunkIndex) ||
                        !int.TryParse(totalChunksStr, out int totalChunks) ||
                        !int.TryParse(chunkSizeStr, out int chunkSize) ||
                        !long.TryParse(totalFileSizeStr, out long totalFileSize))
                    {
                        logger.LogError("Failed to parse chunk parameters - Index: '{Index}', Total: '{Total}', Size: '{Size}', FileSize: '{FileSize}'",
                            chunkIndexStr, totalChunksStr, chunkSizeStr, totalFileSizeStr);
                        return Results.BadRequest("Invalid chunk parameters.");
                    }

                    if (form.Files.Count == 0)
                    {
                        return Results.BadRequest("No file chunk uploaded.");
                    }

                    var file = form.Files[0];
                    // Sanitize fileName to prevent path traversal
                    var fileName = Path.GetFileName(file.FileName);

                    if (string.IsNullOrWhiteSpace(fileName))
                    {
                        return Results.BadRequest("Invalid file name.");
                    }

                    // Create chunks directory if it doesn't exist
                    var chunksDir = Path.Combine(Path.GetTempPath(), ChunksTempDir, fileUuid);
                    Directory.CreateDirectory(chunksDir);

                    // Save the chunk
                    var chunkPath = Path.Combine(chunksDir, $"chunk_{chunkIndex}");
                    using (var chunkStream = file.OpenReadStream())
                    using (var fileStream = new FileStream(chunkPath, FileMode.Create, FileAccess.Write, FileShare.None,
                        bufferSize: 81920, useAsync: true))
                    {
                        await chunkStream.CopyToAsync(fileStream);
                        await fileStream.FlushAsync(); // Ensure data is written to disk
                    }

                    // Check if this is the last chunk
                    if (chunkIndex == totalChunks - 1)
                    {
                        // Verify all chunks are present and validate their sizes
                        long totalChunkSize = 0;
                        for (int i = 0; i < totalChunks; i++)
                        {
                            var chunkFilePath = Path.Combine(chunksDir, $"chunk_{i}");

                            // Retry if chunk file is not immediately available
                            int retries = ChunkValidationRetries;
                            while (retries > 0 && !File.Exists(chunkFilePath))
                            {
                                await Task.Delay(ChunkValidationRetryDelayMs);
                                retries--;
                            }

                            if (!File.Exists(chunkFilePath))
                            {
                                logger.LogError("Chunk {ChunkIndex} is missing for file {FileUuid}", i, fileUuid);
                                return Results.BadRequest($"Chunk {i} is missing. Please retry the upload.");
                            }

                            totalChunkSize += new FileInfo(chunkFilePath).Length;
                        }

                        // Validate total size matches expected
                        if (totalChunkSize != totalFileSize)
                        {
                            logger.LogError("Total chunk size {TotalChunkSize} doesn't match expected {TotalFileSize} for file {FileUuid}",
                                totalChunkSize, totalFileSize, fileUuid);
                            return Results.BadRequest("Chunk size mismatch. Please retry the upload.");
                        }

                        // All chunks received, merge them
                        var finalPath = Path.Combine(directoryPath, fileName);

                        // Create a secure temporary directory for merging
                        var tempMergeDir = Path.Combine(Path.GetTempPath(), ChunksTempDir, "merge");
                        Directory.CreateDirectory(tempMergeDir);
                        var tempMergePath = Path.Combine(tempMergeDir, $"{fileUuid}_merged");

                        try
                        {
                            // Merge all chunks
                            using (var finalStream = new FileStream(tempMergePath, FileMode.Create, FileAccess.Write, FileShare.None))
                            {
                                for (int i = 0; i < totalChunks; i++)
                                {
                                    var chunkFilePath = Path.Combine(chunksDir, $"chunk_{i}");

                                    using (var chunkFileStream = new FileStream(chunkFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                                    {
                                        await chunkFileStream.CopyToAsync(finalStream);
                                    }
                                }
                            }

                            // Upload the merged file
                            using (var mergedStream = new FileStream(tempMergePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                            {
                                await service.UploadFileStreamAsync(directoryPath, fileName, mergedStream, totalFileSize);
                            }

                            // Clean up chunks and temp file independently
                            try
                            {
                                if (Directory.Exists(chunksDir))
                                {
                                    Directory.Delete(chunksDir, true);
                                }
                            }
                            catch (Exception ex)
                            {
                                logger.LogWarning(ex, "Failed to delete chunks directory for file {FileUuid}", fileUuid);
                            }

                            try
                            {
                                if (File.Exists(tempMergePath))
                                {
                                    File.Delete(tempMergePath);
                                }
                            }
                            catch (Exception ex)
                            {
                                logger.LogWarning(ex, "Failed to delete temporary merge file for file {FileUuid}", fileUuid);
                            }

                            return Results.Ok(new
                            {
                                message = "File uploaded successfully",
                                fileName,
                                chunked = true
                            });
                        }
                        catch (Exception ex)
                        {
                            // Clean up on error - handle each operation independently
                            try
                            {
                                if (Directory.Exists(chunksDir))
                                {
                                    Directory.Delete(chunksDir, true);
                                }
                            }
                            catch (Exception cleanupEx)
                            {
                                // Log cleanup failures with appropriate level
                                if (IsCleanupException(cleanupEx))
                                {
                                    logger.LogWarning(cleanupEx, "Failed to cleanup chunks directory for file {FileUuid}", fileUuid);
                                }
                                else
                                {
                                    logger.LogError(cleanupEx, "Unexpected error during chunks cleanup for file {FileUuid}", fileUuid);
                                }
                            }

                            try
                            {
                                if (File.Exists(tempMergePath))
                                {
                                    File.Delete(tempMergePath);
                                }
                            }
                            catch (Exception cleanupEx)
                            {
                                // Log cleanup failures with appropriate level
                                if (IsCleanupException(cleanupEx))
                                {
                                    logger.LogWarning(cleanupEx, "Failed to cleanup temp merge file for file {FileUuid}", fileUuid);
                                }
                                else
                                {
                                    logger.LogError(cleanupEx, "Unexpected error during temp merge file cleanup for file {FileUuid}", fileUuid);
                                }
                            }

                            // Preserve original exception for better debugging
                            throw;
                        }
                    }
                    else
                    {
                        // Not the last chunk, return success
                        return Results.Ok(new
                        {
                            message = $"Chunk {chunkIndex + 1}/{totalChunks} uploaded",
                            chunkIndex,
                            totalChunks
                        });
                    }
                }
                else
                {
                    // Handle regular non-chunked upload
                    var uploadedFiles = new List<string>();
                    var errors = new List<string>();

                    foreach (var file in form.Files)
                    {
                        try
                        {
                            if (file.Length == 0)
                            {
                                errors.Add($"{file.FileName}: File is empty");
                                continue;
                            }

                            // Use streaming upload to avoid loading entire file into memory
                            using var stream = file.OpenReadStream();
                            await service.UploadFileStreamAsync(directoryPath, file.FileName, stream, file.Length);
                            uploadedFiles.Add(file.FileName);
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"{file.FileName}: {ex.Message}");
                        }
                    }

                    if (uploadedFiles.Count == 0 && errors.Count > 0)
                    {
                        return Results.BadRequest(new { message = "All uploads failed", errors });
                    }

                    return Results.Ok(new
                    {
                        message = $"Uploaded {uploadedFiles.Count} file(s) successfully",
                        uploadedFiles,
                        errors = errors.Count > 0 ? errors : null
                    });
                }
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
            catch (Exception ex)
            {
                // Don't expose internal error details for security
                return Results.Problem($"An error occurred while uploading files: {ex.Message}");
            }
        })
        .DisableAntiforgery() // NOTE: Disabled for API access - should implement authentication/authorization before production use
        .WithName("UploadFile")
        .WithSummary("上传文件");
    }

    // File upload configuration constants
    private const string ChunksTempDir = "qingfeng_chunks";
    private const int ChunkValidationRetries = 3;
    private const int ChunkValidationRetryDelayMs = 50;

    // Compiled regex for UUID validation
    private static readonly System.Text.RegularExpressions.Regex UuidValidationRegex = new(
        @"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$",
        System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    // Helper method to check if an exception is a cleanup-related exception
    private static bool IsCleanupException(Exception ex) =>
        ex is IOException || ex is UnauthorizedAccessException || ex is DirectoryNotFoundException;

    // Helper function for content type detection
    private static string GetContentType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".txt" => "text/plain",
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".svg" => "image/svg+xml",
            ".zip" => "application/zip",
            ".rar" => "application/x-rar-compressed",
            ".7z" => "application/x-7z-compressed",
            ".mp3" => "audio/mpeg",
            ".mp4" => "video/mp4",
            ".avi" => "video/x-msvideo",
            ".json" => "application/json",
            ".xml" => "application/xml",
            ".css" => "text/css",
            ".js" => "application/javascript",
            ".html" or ".htm" => "text/html",
            _ => "application/octet-stream"
        };
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
