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

// Register custom services
builder.Services.AddSingleton<ISystemMonitorService, SystemMonitorService>();
builder.Services.AddSingleton<IDockerService, DockerService>();
builder.Services.AddScoped<IFileManagerService, FileManagerService>();
builder.Services.AddSingleton<IDiskManagementService, DiskManagementService>();
builder.Services.AddScoped<IHomeConfigService, HomeConfigService>();
builder.Services.AddScoped<IDialogService, DialogService>();

var app = builder.Build();

// Initialize database
using (var scope = app.Services.CreateScope())
{
    var homeConfigService = scope.ServiceProvider.GetRequiredService<IHomeConfigService>();
    await homeConfigService.InitializeDefaultsAsync();
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
