using System;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace RepPortal.Data;

public sealed class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    private readonly string? _policy;

    // If you pass null or omit the policy, it only requires authentication.
    public HangfireAuthorizationFilter(string? policy = null) => _policy = policy;

    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        var user = httpContext?.User;

        if (user?.Identity?.IsAuthenticated != true)
            return false;

        // No policy specified -> authenticated users are allowed.
        if (string.IsNullOrEmpty(_policy))
            return true;

        // Enforce an authorization policy. Must block on the async call here.
        var authz = httpContext!.RequestServices.GetRequiredService<IAuthorizationService>();
        var result = authz.AuthorizeAsync(user, resource: null, _policy).GetAwaiter().GetResult();
        return result.Succeeded;
    }
}
