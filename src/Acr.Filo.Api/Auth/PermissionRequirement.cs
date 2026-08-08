using Microsoft.AspNetCore.Authorization;

namespace Acr.Filo.Api.Auth;

/// <summary>Yetki bazlı policy. [Authorize(Policy="orders.create")] gibi kullanılır.</summary>
public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public string Permission { get; }
    public PermissionRequirement(string permission) => Permission = permission;
}

public sealed class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement req)
    {
        if (context.User.HasClaim("perm", req.Permission))
            context.Succeed(req);
        return Task.CompletedTask;
    }
}

/// <summary>"perm" claim'ine göre policy'leri otomatik üretir (her yetki için ayrı policy tanımlamaya gerek yok).</summary>
public sealed class PermissionPolicyProvider : Microsoft.AspNetCore.Authorization.IAuthorizationPolicyProvider
{
    private readonly Microsoft.AspNetCore.Authorization.DefaultAuthorizationPolicyProvider _fallback;
    public PermissionPolicyProvider(Microsoft.Extensions.Options.IOptions<AuthorizationOptions> options)
        => _fallback = new Microsoft.AspNetCore.Authorization.DefaultAuthorizationPolicyProvider(options);

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        // Yetki anahtarı formatı: nokta içerir (orders.create). Bunları policy'ye çevir.
        if (policyName.Contains('.'))
        {
            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(policyName))
                .Build();
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }
        return _fallback.GetPolicyAsync(policyName);
    }
}
