namespace BuildingCopilot.Infrastructure.Configuration;

public sealed class OllamaOptions
{
    public const string SectionName = "Ollama";

    public string BaseUrl { get; init; } = "http://localhost:11434/";
    public string ChatModel { get; init; } = "llama3.2:3b";
    public string EmbeddingModel { get; init; } = "nomic-embed-text";
}