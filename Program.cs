using QingFeng.Components;
using QingFeng.Services;
using QingFeng.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add SQLite database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Data Source=qingfeng.db";
builder.Services.AddDbContext<QingFengDbContext>(options =>
    options.UseSqlite(connectionString));

// Add localization services
builder.Services.AddLocalization();

// Register custom services
builder.Services.AddSingleton<ISystemMonitorService, SystemMonitorService>();
builder.Services.AddSingleton<IDockerService, DockerService>();
builder.Services.AddScoped<IFileManagerService, FileManagerService>();
builder.Services.AddSingleton<IDiskManagementService, DiskManagementService>();
builder.Services.AddScoped<IDockItemService, DockItemService>();
builder.Services.AddScoped<IApplicationService, ApplicationService>();
builder.Services.AddScoped<ISystemSettingService, SystemSettingService>();
builder.Services.AddSingleton<IDialogService, DialogService>();
builder.Services.AddScoped<ILocalizationService, LocalizationService>();

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

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
