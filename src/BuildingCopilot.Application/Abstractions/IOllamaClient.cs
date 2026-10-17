namespace BuildingCopilot.Application.Abstractions;

public interface IOllamaClient
{
    Task<string> CompleteAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken);

    Task<float[]> CreateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken);
}