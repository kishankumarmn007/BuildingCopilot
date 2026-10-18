namespace BuildingCopilot.Infrastructure.Configuration;

public sealed class QdrantOptions
{
    public const string SectionName = "Qdrant";

    public string BaseUrl { get; init; } = "http://localhost:6333/";
    public string CollectionName { get; init; } = "buildingcopilot-knowledge";
    public string? ApiKey { get; init; }
}