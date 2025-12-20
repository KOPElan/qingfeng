using Microsoft.AspNetCore.Components;
using QingFeng.Models;
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
    private bool _hasCheckedAuth = false;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && !_hasCheckedAuth)
        {
            await CheckAuthorizationAsync();
        }
        await base.OnAfterRenderAsync(firstRender);
    }

    protected virtual async Task CheckAuthorizationAsync()
    {
        _hasCheckedAuth = true;
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
        if (RequiresAdmin && currentUser.Role != User.RoleAdmin)
        {
            // Not authorized, redirect to home
            Navigation.NavigateTo("/", forceLoad: true);
            return;
        }

        IsAuthorized = true;
        IsLoading = false;
        StateHasChanged();
    }

    protected virtual bool RequiresAdmin => true;
}
