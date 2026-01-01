using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using QingFeng.Models;

namespace QingFeng.Services;

public class AuthenticationStateService
{
    private readonly ProtectedLocalStorage _localStorage;
    private User? _currentUser;
    private bool _initialized = false;
    private const string StorageKey = "CurrentUser";

    public AuthenticationStateService(ProtectedLocalStorage localStorage)
    {
        _localStorage = localStorage;
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
            await _localStorage.SetAsync(StorageKey, user);
        }
        else
        {
            await _localStorage.DeleteAsync(StorageKey);
        }
    }

    public async Task ClearAsync()
    {
        _currentUser = null;
        _initialized = true;
        await _localStorage.DeleteAsync(StorageKey);
    }

    private async Task InitializeAsync()
    {
        try
        {
            var result = await _localStorage.GetAsync<User>(StorageKey);
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
