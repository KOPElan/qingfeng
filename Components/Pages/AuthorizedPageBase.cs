using Microsoft.AspNetCore.Components;
using QingFeng.Services;

namespace QingFeng.Components.Pages;

public class AuthorizedPageBase : ComponentBase
{
    [Inject]
    protected IAuthenticationService AuthService { get; set; } = default!;

    [Inject]
    protected NavigationManager Navigation { get; set; } = default!;

    protected bool IsAuthorized { get; private set; }
    protected bool IsLoading { get; private set; } = true;
    protected string? CurrentUsername { get; private set; }

    protected override async Task OnInitializedAsync()
    {
        await CheckAuthorizationAsync();
        await base.OnInitializedAsync();
    }

    protected virtual async Task CheckAuthorizationAsync()
    {
        IsLoading = true;
        
        var currentUser = await AuthService.GetCurrentUserAsync();
        if (currentUser == null)
        {
            // Not logged in, redirect to login
            Navigation.NavigateTo($"/login?returnUrl={Uri.EscapeDataString(Navigation.Uri)}", forceLoad: true);
            return;
        }

        CurrentUsername = currentUser.Username;
        
        // Check if user is admin (for admin-only pages)
        if (RequiresAdmin && currentUser.Role != "Admin")
        {
            // Not authorized, redirect to home
            Navigation.NavigateTo("/", forceLoad: true);
            return;
        }

        IsAuthorized = true;
        IsLoading = false;
    }

    protected virtual bool RequiresAdmin => true;
}
