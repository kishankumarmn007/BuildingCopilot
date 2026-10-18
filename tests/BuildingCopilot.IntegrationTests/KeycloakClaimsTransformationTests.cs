using System.Security.Claims;
using BuildingCopilot.Api.Authentication;

namespace BuildingCopilot.IntegrationTests;

public sealed class KeycloakClaimsTransformationTests
{
    [Fact]
    public async Task TransformAsync_MapsRealmRolesToAspNetRoleClaims()
    {
        var identity = new ClaimsIdentity("test");
        identity.AddClaim(new Claim("realm_access", "{\"roles\":[\"building-user\",\"building-admin\"]}"));
        var principal = new ClaimsPrincipal(identity);

        var transformed = await new KeycloakClaimsTransformation().TransformAsync(principal);

        Assert.True(transformed.IsInRole("building-user"));
        Assert.True(transformed.IsInRole("building-admin"));
    }

    [Fact]
    public async Task TransformAsync_IsIdempotent()
    {
        var identity = new ClaimsIdentity("test");
        identity.AddClaim(new Claim("realm_access", "{\"roles\":[\"building-user\"]}"));
        var principal = new ClaimsPrincipal(identity);
        var transformation = new KeycloakClaimsTransformation();

        await transformation.TransformAsync(principal);
        await transformation.TransformAsync(principal);

        Assert.Single(principal.FindAll(ClaimTypes.Role));
    }
}