# Copilot / AI Agent Instructions for QingFeng

This file gives concise, repository-specific guidance so AI coding agents can be immediately productive working on QingFeng.

Quick facts
- **Framework**: .NET 10 (see `QingFeng.csproj` TargetFramework)
- **App type**: Blazor Server with SignalR-based terminal and REST endpoints (`Program.cs`)
- **DB**: EF Core + SQLite (`Data/QingFengDbContext.cs`, `Migrations/`)

Priority patterns & conventions
- Use `IDbContextFactory<QingFengDbContext>` for services that require creating contexts on demand. Example: `FileManagerService` uses `IDbContextFactory` (`Services/FileManagerService.cs`).
- There's also a scoped DbContext registered for classic DI consumers; prefer factory for new services to control lifetime (`Program.cs`).
- File access is gated by `FileManagerService.IsPathAllowed(...)`. Any code that reads/writes files must call the same checks or reuse `IFileManagerService`.
- File uploads use streaming to avoid OOM; buffer size constant: `StreamBufferSize = 81920` (`FileManagerService`). Follow streaming pattern for large-file work.
- Authentication/authorization is lightweight and in-progress: `AuthenticationService` persists session state via `AuthenticationStateService`. `Program.cs` currently disables antiforgery for upload endpoint and relies on `FileManagerService.IsPathAllowed()` for path security—treat endpoints as unauthenticated until user management is implemented.
- Database migrations are applied automatically at startup (`dbContext.Database.MigrateAsync()` in `Program.cs`). Use `dotnet ef` only when modifying migrations.

Important files to reference
- App startup & routes: `Program.cs` (service registrations, endpoints, SignalR hub mapping)
- Data model & constraints: `Data/QingFengDbContext.cs`
- File management: `Services/FileManagerService.cs` (path rules, streaming, favorites)
- Authentication: `Services/AuthenticationService.cs` (password hashing, admin checks)
- Terminal: `Hubs/TerminalHub.cs` (SignalR integration, xterm.js client)
- Components & UI: `Components/` (Blazor pages & layouts)

Developer workflows (commands)
- Restore: `dotnet restore`
- Run locally (defaults to SQLite `qingfeng.db`):
  - `dotnet run`
  - or `dotnet run --urls "http://0.0.0.0:5000"`
- Build: `dotnet build`
- Publish: `dotnet publish -c Release -o ./publish`
- Database migrations: use EF tools when adding migrations. At runtime the app auto-applies migrations.

Security & OS notes
- File root behavior: on Windows the root for file manager is the user profile; on Linux root `/` is allowed. See `FileManagerService` constructor.
- Disk management features are Linux-specific and require external tools (`lsblk`, `mount`, `hdparm`, `cifs-utils`, `nfs-common`). README documents recommended packages and sudo usage.
- Docker: project references `Docker.DotNet`. Docker connection uses default sockets (`unix:///var/run/docker.sock` or `npipe://./pipe/docker_engine`) or `DOCKER_HOST` env.

Guidance for common AI tasks
- Adding a new service that needs DB access: register service in `Program.cs` and prefer `IDbContextFactory<T>` + `CreateDbContextAsync()` inside the service to avoid scoped-lifetime issues.
- Modifying file upload/download: keep streaming pattern and reuse `IFileManagerService` for path validation and security checks; do not bypass `IsPathAllowed`.
- Adding auth gates: note `Program.cs` has TODOs for auth; adding authentication should remove the `DisableAntiforgery()` call on upload endpoint and secure the download/upload endpoints.
- Modifying migrations/tables: update `Data/QingFengDbContext.cs` then create EF migration and verify runtime `MigrateAsync()` applies it.

Quick examples (copyable)
- Create a DbContext in a service:
```csharp
using var ctx = await _dbContextFactory.CreateDbContextAsync();
// use ctx
```
- Validate path reuse:
```csharp
if (!fileManager.IsPathAllowed(path)) throw new UnauthorizedAccessException();
```

What not to assume
- Endpoints are not yet protected by full authentication; do not add public-facing secrets or assume antiforgery is enabled.
- Tests are not present in the repo root; don't presume an existing test harness unless you add one.

If anything here is unclear or you want more examples (code snippets for migration, auth integration, or file streaming tests), tell me which area to expand. 
