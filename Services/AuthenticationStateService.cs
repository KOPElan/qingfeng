using QingFeng.Models;

namespace QingFeng.Services;

public class AuthenticationStateService
{
    private User? _currentUser;

    public User? CurrentUser
    {
        get => _currentUser;
        set => _currentUser = value;
    }

    public bool IsAuthenticated => _currentUser != null;
    
    public bool IsAdmin => _currentUser?.Role == "Admin";

    public void SetUser(User? user)
    {
        _currentUser = user;
    }

    public void Clear()
    {
        _currentUser = null;
    }
}
