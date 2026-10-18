namespace BuildingCopilot.Api.Authentication;

public sealed class AuthenticationOptions
{
    public const string SectionName = "Authentication";

    public string Authority { get; init; } = "http://localhost:8081/realms/building-copilot";
    public string Issuer { get; init; } = "http://localhost:8081/realms/building-copilot";
    public string? MetadataAddress { get; init; }
    public string? BackchannelAuthority { get; init; }
    public string Audience { get; init; } = "buildingcopilot-api";
    public bool RequireHttpsMetadata { get; init; } = true;
}