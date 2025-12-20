using QingFeng.Models;

namespace QingFeng.Services;

public interface IAuthenticationService
{
    Task<User?> LoginAsync(string username, string password);
    Task LogoutAsync();
    Task<User> CreateUserAsync(string username, string password, string role);
    Task<bool> HasAdminUserAsync();
    Task<User?> GetCurrentUserAsync();
    Task<bool> IsAdminAsync();
    string? GetCurrentUsername();
    Task<List<User>> GetAllUsersAsync();
    Task DeleteUserAsync(int userId);
}
