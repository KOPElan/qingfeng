using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using QingFeng.Data;
using QingFeng.Models;

namespace QingFeng.Services;

public class AuthenticationService : IAuthenticationService
{
    private readonly QingFengDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private const string SessionKeyUsername = "Username";
    private const string SessionKeyUserId = "UserId";
    private const string SessionKeyUserRole = "UserRole";

    public AuthenticationService(QingFengDbContext dbContext, IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<User?> LoginAsync(string username, string password)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Username == username && u.IsActive);

        if (user == null)
            return null;

        var passwordHash = HashPassword(password);
        if (user.PasswordHash != passwordHash)
            return null;

        // Update last login time
        user.LastLoginAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        // Set session
        var session = _httpContextAccessor.HttpContext?.Session;
        if (session != null)
        {
            session.SetString(SessionKeyUsername, user.Username);
            session.SetInt32(SessionKeyUserId, user.Id);
            session.SetString(SessionKeyUserRole, user.Role);
        }

        return user;
    }

    public Task LogoutAsync()
    {
        var session = _httpContextAccessor.HttpContext?.Session;
        session?.Clear();
        return Task.CompletedTask;
    }

    public async Task<User> CreateUserAsync(string username, string password, string role)
    {
        // Check if user already exists
        var existingUser = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Username == username);

        if (existingUser != null)
            throw new InvalidOperationException("用户名已存在");

        var user = new User
        {
            Username = username,
            PasswordHash = HashPassword(password),
            Role = role,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        return user;
    }

    public async Task<bool> HasAdminUserAsync()
    {
        return await _dbContext.Users.AnyAsync(u => u.Role == "Admin" && u.IsActive);
    }

    public async Task<User?> GetCurrentUserAsync()
    {
        var username = GetCurrentUsername();
        if (string.IsNullOrEmpty(username))
            return null;

        return await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Username == username && u.IsActive);
    }

    public async Task<bool> IsAdminAsync()
    {
        var user = await GetCurrentUserAsync();
        return user?.Role == "Admin";
    }

    public string? GetCurrentUsername()
    {
        var session = _httpContextAccessor.HttpContext?.Session;
        return session?.GetString(SessionKeyUsername);
    }

    public async Task<List<User>> GetAllUsersAsync()
    {
        return await _dbContext.Users
            .OrderBy(u => u.Username)
            .ToListAsync();
    }

    public async Task DeleteUserAsync(int userId)
    {
        var user = await _dbContext.Users.FindAsync(userId);
        if (user == null)
            throw new InvalidOperationException("用户不存在");

        if (user.Role == "Admin")
        {
            // Check if this is the last admin
            var adminCount = await _dbContext.Users.CountAsync(u => u.Role == "Admin" && u.IsActive);
            if (adminCount <= 1)
                throw new InvalidOperationException("不能删除最后一个管理员账号");
        }

        _dbContext.Users.Remove(user);
        await _dbContext.SaveChangesAsync();
    }

    private static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(password);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}
