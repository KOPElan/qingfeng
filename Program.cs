using QingFeng.Components;
using QingFeng.Services;
using QingFeng.Data;
using QingFeng.Hubs;
using Microsoft.EntityFrameworkCore;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.AspNetCore.Http.Features;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);

// File upload configuration constants
const long MAX_FILE_SIZE = 2147483648; // 2GB
const string CHUNKS_TEMP_DIR = "qingfeng_chunks";
const int CHUNK_VALIDATION_RETRIES = 3; // Number of retries for chunk validation
const int CHUNK_VALIDATION_RETRY_DELAY_MS = 50; // Delay between retries

// Compiled regex for UUID validation
// Accepts UUID v4 format and similar patterns from Dropzone.js
// Pattern: 8-4-4-4-12 hex digits with hyphens
var uuidValidationRegex = new Regex(@"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$", 
    RegexOptions.Compiled | RegexOptions.IgnoreCase);

// Helper method to check if an exception is a cleanup-related exception
static bool IsCleanupException(Exception ex) => 
    ex is IOException || ex is UnauthorizedAccessException || ex is DirectoryNotFoundException;

// Configure Kestrel for large file uploads
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = MAX_FILE_SIZE;
});

// Configure FormOptions for large multipart uploads
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = MAX_FILE_SIZE;
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartHeadersLengthLimit = int.MaxValue;
});

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add Fluent UI services
builder.Services.AddFluentUIComponents();

// Add SignalR for terminal
builder.Services.AddSignalR();

builder.Services.AddHttpContextAccessor();

// Add SQLite database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Data Source=qingfeng.db";
// Register DbContextFactory for services that need multiple contexts or custom lifetime management (e.g., FileManagerService)
builder.Services.AddDbContextFactory<QingFengDbContext>(options =>
    options.UseSqlite(connectionString));
// Also register scoped DbContext for services using traditional injection pattern (e.g., DockItemService, ApplicationService)
// Prefer DbContextFactory for new services to allow better control over context lifetime
builder.Services.AddScoped(provider => provider.GetRequiredService<IDbContextFactory<QingFengDbContext>>().CreateDbContext());

// Add localization services
builder.Services.AddLocalization();

// Register custom services
builder.Services.AddSingleton<ISystemMonitorService, SystemMonitorService>();
builder.Services.AddSingleton<IDockerService, DockerService>();
builder.Services.AddScoped<IFileManagerService, FileManagerService>();
builder.Services.AddSingleton<IDiskManagementService, DiskManagementService>();
builder.Services.AddSingleton<IShareManagementService, ShareManagementService>();
builder.Services.AddSingleton<ITerminalService, TerminalService>();
builder.Services.AddScoped<IDockItemService, DockItemService>();
builder.Services.AddScoped<IApplicationService, ApplicationService>();
builder.Services.AddScoped<ISystemSettingService, SystemSettingService>();
builder.Services.AddScoped<ILocalizationService, LocalizationService>();
builder.Services.AddScoped<AuthenticationStateService>();
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<IFileIndexService, FileIndexService>();
builder.Services.AddScoped<IScheduledTaskService, ScheduledTaskService>();
builder.Services.AddHostedService<ScheduledTaskExecutorService>();

var app = builder.Build();

// Initialize database
using (var scope = app.Services.CreateScope())
{
    try
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<QingFengDbContext>();
        await dbContext.Database.MigrateAsync();
        
        var dockItemService = scope.ServiceProvider.GetRequiredService<IDockItemService>();
        await dockItemService.InitializeDefaultDockItemsAsync();
        
        var applicationService = scope.ServiceProvider.GetRequiredService<IApplicationService>();
        await applicationService.InitializeDefaultApplicationsAsync();
        
        var systemSettingService = scope.ServiceProvider.GetRequiredService<ISystemSettingService>();
        await systemSettingService.InitializeDefaultSettingsAsync();
        
        // Check if initial setup is needed
        var authService = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();
        var hasAdmin = await authService.HasAdminUserAsync();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        if (!hasAdmin)
        {
            logger.LogInformation("No admin user found. Initial setup is required.");
        }
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogCritical(ex, "Failed to initialize database. The application cannot start without a valid database connection. Please check the connection string and ensure the database is accessible.");
        throw;
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

// Add file download endpoint
// TODO: Add authentication/authorization when implementing user management
// Currently relies on FileManagerService.IsPathAllowed() for security
app.MapGet("/api/files/download", async (string path, IFileManagerService fileManager) =>
{
    try
    {
        var fileBytes = await fileManager.DownloadFileAsync(path);
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
});

// Add file upload endpoint with streaming support
// Supports both regular uploads and chunked uploads for large files
// TODO: Add authentication/authorization when implementing user management
// Note: Currently relies on FileManagerService.IsPathAllowed() for security
// Antiforgery is disabled to support external API clients - consider enabling with proper auth
app.MapPost("/api/files/upload", async (HttpRequest request, IFileManagerService fileManager, ILogger<Program> logger) =>
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
            if (string.IsNullOrWhiteSpace(fileUuid) || !uuidValidationRegex.IsMatch(fileUuid))
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
            var chunksDir = Path.Combine(Path.GetTempPath(), CHUNKS_TEMP_DIR, fileUuid);
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
                    int retries = CHUNK_VALIDATION_RETRIES;
                    while (retries > 0 && !File.Exists(chunkFilePath))
                    {
                        await Task.Delay(CHUNK_VALIDATION_RETRY_DELAY_MS);
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
                var tempMergeDir = Path.Combine(Path.GetTempPath(), CHUNKS_TEMP_DIR, "merge");
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
                        await fileManager.UploadFileStreamAsync(directoryPath, fileName, mergedStream, totalFileSize);
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
                    await fileManager.UploadFileStreamAsync(directoryPath, file.FileName, stream, file.Length);
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
.DisableAntiforgery(); // NOTE: Disabled for API access - should implement authentication/authorization before production use

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Map SignalR hub for terminal
app.MapHub<TerminalHub>("/terminalhub");

// Helper function for content type detection
static string GetContentType(string fileName)
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

app.Run();
