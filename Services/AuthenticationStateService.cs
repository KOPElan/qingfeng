using Microsoft.AspNetCore.Http;
using QingFeng.Models;

namespace QingFeng.Services;

public class AuthenticationStateService
{
    private const string SessionKeyUserId = "UserId";
    private readonly IHttpContextAccessor _httpContextAccessor;
    private User? _currentUser;

    public AuthenticationStateService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public User? CurrentUser
    {
        get => _currentUser;
        set => _currentUser = value;
    }

    public bool IsAuthenticated => _currentUser != null;
    
    public bool IsAdmin => _currentUser?.Role == User.RoleAdmin;

    public void SetUser(User? user)
    {
        _currentUser = user;
        
        // Persist to session
        var session = _httpContextAccessor.HttpContext?.Session;
        if (session != null)
        {
            if (user != null)
            {
                session.SetInt32(SessionKeyUserId, user.Id);
            }
            else
            {
                session.Remove(SessionKeyUserId);
            }
        }
    }

    public int? GetUserIdFromSession()
    {
        var session = _httpContextAccessor.HttpContext?.Session;
        return session?.GetInt32(SessionKeyUserId);
    }

    public void Clear()
    {
        _currentUser = null;
        
        // Clear from session
        var session = _httpContextAccessor.HttpContext?.Session;
        session?.Remove(SessionKeyUserId);
    }
}
