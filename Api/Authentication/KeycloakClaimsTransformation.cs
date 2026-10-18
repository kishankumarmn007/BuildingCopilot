using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;

namespace BuildingCopilot.Api.Authentication;

public sealed class KeycloakClaimsTransformation : IClaimsTransformation
{
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        var realmAccess = principal.FindFirst("realm_access")?.Value;
        if (string.IsNullOrWhiteSpace(realmAccess))
        {
            return Task.FromResult(principal);
        }

        using var json = JsonDocument.Parse(realmAccess);
        if (!json.RootElement.TryGetProperty("roles", out var roles))
        {
            return Task.FromResult(principal);
        }

        foreach (var role in roles.EnumerateArray())
        {
            var value = role.GetString();
            if (value is not null && !principal.HasClaim(ClaimTypes.Role, value))
            {
                ((ClaimsIdentity)principal.Identity!).AddClaim(new Claim(ClaimTypes.Role, value));
            }
        }

        return Task.FromResult(principal);
    }
}