using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using QingFeng.Models;

namespace QingFeng.Services;

public class AuthenticationStateService
{
    private readonly ProtectedSessionStorage _sessionStorage;
    private User? _currentUser;
    private bool _initialized = false;
    private const string StorageKey = "CurrentUser";

    public AuthenticationStateService(ProtectedSessionStorage sessionStorage)
    {
        _sessionStorage = sessionStorage;
    }

    public async Task<User?> GetCurrentUserAsync()
    {
        if (!_initialized)
        {
            await InitializeAsync();
        }
        return _currentUser;
    }

    public bool IsAuthenticated => _currentUser != null;
    
    public bool IsAdmin => _currentUser?.Role == User.RoleAdmin;

    public async Task SetUserAsync(User? user)
    {
        _currentUser = user;
        _initialized = true;
        
        if (user != null)
        {
            await _sessionStorage.SetAsync(StorageKey, user);
        }
        else
        {
            await _sessionStorage.DeleteAsync(StorageKey);
        }
    }

    public async Task ClearAsync()
    {
        _currentUser = null;
        _initialized = true;
        await _sessionStorage.DeleteAsync(StorageKey);
    }

    private async Task InitializeAsync()
    {
        try
        {
            var result = await _sessionStorage.GetAsync<User>(StorageKey);
            _currentUser = result.Success ? result.Value : null;
        }
        catch
        {
            _currentUser = null;
        }
        finally
        {
            _initialized = true;
        }
    }
}
