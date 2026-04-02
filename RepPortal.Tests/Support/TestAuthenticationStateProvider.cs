using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace RepPortal.Tests.Support;

internal sealed class TestAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly AuthenticationState _state;

    public TestAuthenticationStateProvider(params Claim[] claims)
    {
        var identity = claims.Length == 0
            ? new ClaimsIdentity()
            : new ClaimsIdentity(claims, authenticationType: "Test");

        _state = new AuthenticationState(new ClaimsPrincipal(identity));
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
        => Task.FromResult(_state);
}
