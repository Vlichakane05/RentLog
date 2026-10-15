using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using RentLog.Models;

namespace RentLog.Services;

public class RentLogAuthStateProvider : AuthenticationStateProvider
{
    private readonly IAuthService _authService;

    public RentLogAuthStateProvider(IAuthService authService)
    {
        _authService = authService;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var user = await _authService.GetCurrentUserAsync();

        if (user == null)
        {
            return new AuthenticationState(new ClaimsPrincipal());
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.Name),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };

        var identity = new ClaimsIdentity(claims, "RentLog");
        var principal = new ClaimsPrincipal(identity);

        return new AuthenticationState(principal);
    }

    public async Task LoginAsync(AuthUser user)
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public async Task LogoutAsync()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }
}
