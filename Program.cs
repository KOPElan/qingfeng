using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.FluentUI.AspNetCore.Components;
using QingFeng.Components;
using QingFeng.Data;
using QingFeng.Endpoints;
using QingFeng.Hubs;
using QingFeng.Services;

var builder = WebApplication.CreateBuilder(args);

// File upload configuration constants
const long MAX_FILE_SIZE = 2147483648; // 2GB

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

// Add HttpClient factory for services that need to make HTTP requests
builder.Services.AddHttpClient();

// Add SQLite database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=qingfeng.db";
// Register DbContextFactory for services that need multiple contexts or custom lifetime management (e.g., FileManagerService)
builder.Services.AddDbContextFactory<QingFengDbContext>(options =>
    options.UseSqlite(connectionString));
// Prefer IDbContextFactory for new services to allow better control over context lifetime

// Add localization services
builder.Services.AddLocalization();

// Register custom services
builder.Services.AddSingleton<ISystemMonitorService, SystemMonitorService>();
builder.Services.AddSingleton<IDockerService, DockerService>();
builder.Services.AddScoped<IFileManagerService, FileManagerService>();
builder.Services.AddSingleton<IDiskManagementService, DiskManagementService>();
builder.Services.AddSingleton<IShareManagementService, ShareManagementService>();
builder.Services.AddSingleton<ITerminalService, TerminalService>();
builder.Services.AddSingleton<INetworkManagementService, NetworkManagementService>();
builder.Services.AddScoped<IDockItemService, DockItemService>();
builder.Services.AddScoped<IApplicationService, ApplicationService>();
builder.Services.AddScoped<ISystemSettingService, SystemSettingService>();
builder.Services.AddScoped<ILocalizationService, LocalizationService>();
// Use ProtectedLocalStorage so auth persists across browser restarts
builder.Services.AddScoped<AuthenticationStateService>();
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<IFileIndexService, FileIndexService>();
builder.Services.AddScoped<IScheduledTaskService, ScheduledTaskService>();
builder.Services.AddScoped<IScheduledTaskExecutionHistoryService, ScheduledTaskExecutionHistoryService>();
builder.Services.AddHostedService<ScheduledTaskExecutorService>();
builder.Services.AddScoped<IAnydropService, AnydropService>();
builder.Services.AddScoped<IThumbnailService, ThumbnailService>();

var app = builder.Build();

// Initialize database
using (var scope = app.Services.CreateScope())
{
    try
    {
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<QingFengDbContext>>();
        using var dbContext = dbFactory.CreateDbContext();
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

// Map all API endpoints
app.MapSystemMonitorEndpoints();
app.MapDockerEndpoints();
app.MapFileManagerEndpoints();
app.MapDiskManagementEndpoints();
app.MapShareManagementEndpoints();
app.MapAuthenticationEndpoints();
app.MapTerminalEndpoints();
app.MapAnydropEndpoints();
app.MapApplicationEndpoints();
app.MapDockItemEndpoints();
app.MapScheduledTaskEndpoints();
app.MapSystemSettingEndpoints();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Map SignalR hub for terminal
app.MapHub<TerminalHub>("/terminalhub");

app.Run();
