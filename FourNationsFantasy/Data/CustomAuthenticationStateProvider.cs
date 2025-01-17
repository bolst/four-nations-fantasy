using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace FourNationsFantasy.Data;

public class CustomAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly CustomUserService _userService;
    public User CurrentUser { get; private set; } = new();

    public CustomAuthenticationStateProvider(CustomUserService userService)
    {
        _userService = userService;
        AuthenticationStateChanged += OnAuthenticationStateChangedAsync;
    }

    private async void OnAuthenticationStateChangedAsync(Task<AuthenticationState> task)
    {
        var authState = await task;

        if (authState.User.Identity is not null)
        {
            CurrentUser = User.FromClaimsPrincipal(authState.User);
        }
    }
    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var principal = new ClaimsPrincipal();
        User? user = await _userService.FetchUserFromBrowserAsync();

        if (user?.Email is not null)
        {
            User? userInDb = await _userService.LookupUserInDatabase(user.Email);

            if (userInDb is not null)
            {
                principal = userInDb.ToClaimsPrincipal();
                CurrentUser = userInDb;
            }
        }
        
        return new AuthenticationState(principal);
    }
}