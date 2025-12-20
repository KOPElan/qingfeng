using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using QingFeng.Data;
using QingFeng.Models;

namespace QingFeng.Services;

public class AuthenticationService : IAuthenticationService
{
    private readonly QingFengDbContext _dbContext;
    private readonly AuthenticationStateService _authState;

    public AuthenticationService(QingFengDbContext dbContext, AuthenticationStateService authState)
    {
        _dbContext = dbContext;
        _authState = authState;
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

        // Set authentication state
        _authState.SetUser(user);

        return user;
    }

    public Task LogoutAsync()
    {
        _authState.Clear();
        return Task.CompletedTask;
    }

    public async Task<User> CreateUserAsync(string username, string password, string role)
    {
        // Validate username format
        if (!IsValidUsername(username))
            throw new InvalidOperationException("用户名只能包含字母、数字、下划线和连字符");

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
        return await _dbContext.Users.AnyAsync(u => u.Role == User.RoleAdmin && u.IsActive);
    }

    public async Task<User?> GetCurrentUserAsync()
    {
        // Return from circuit-scoped state
        return await Task.FromResult(_authState.CurrentUser);
    }

    public Task<bool> IsAdminAsync()
    {
        return Task.FromResult(_authState.IsAdmin);
    }

    public string? GetCurrentUsername()
    {
        return _authState.CurrentUser?.Username;
    }

    public async Task<List<User>> GetAllUsersAsync()
    {
        return await _dbContext.Users
            .OrderBy(u => u.Username)
            .ToListAsync();
    }

    public async Task DeleteUserAsync(int userId)
    {
        if (userId <= 0)
            throw new ArgumentException("Invalid user ID", nameof(userId));

        var user = await _dbContext.Users.FindAsync(userId);
        if (user == null)
            throw new InvalidOperationException("用户不存在");

        if (user.Role == User.RoleAdmin)
        {
            // Check if this is the last admin
            var adminCount = await _dbContext.Users.CountAsync(u => u.Role == User.RoleAdmin && u.IsActive);
            if (adminCount <= 1)
                throw new InvalidOperationException("不能删除最后一个管理员账号");
        }

        _dbContext.Users.Remove(user);
        await _dbContext.SaveChangesAsync();

        // Invalidate authentication state if the deleted user is currently authenticated
        if (_authState.CurrentUser != null && _authState.CurrentUser.Id == userId)
        {
            _authState.Clear();
        }
    }

    private static bool IsValidUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return false;

        // Only allow alphanumeric, underscore, and hyphen
        return username.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '-');
    }

    private static string HashPassword(string password)
    {
        // Use PBKDF2 with a high iteration count for better security
        const int iterations = 100000;
        const int keySize = 32; // 256-bit key

        // Derive a deterministic salt from the password itself
        // This maintains compatibility with equality-based comparisons
        using var sha256 = SHA256.Create();
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        var salt = sha256.ComputeHash(passwordBytes);

        // Use static Pbkdf2 method (recommended approach)
        var derivedKey = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, keySize);

        return Convert.ToBase64String(derivedKey);
    }
}
