using QingFeng.Components;
using QingFeng.Services;
using QingFeng.Data;
using QingFeng.Hubs;
using Microsoft.EntityFrameworkCore;
using Microsoft.FluentUI.AspNetCore.Components;

var builder = WebApplication.CreateBuilder(args);

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
// This accepts IFormFile directly and uses streaming to avoid loading entire file into memory
app.MapPost("/api/files/upload", async (HttpRequest request, IFileManagerService fileManager) =>
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
.DisableAntiforgery(); // Disable antiforgery for file upload API

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
