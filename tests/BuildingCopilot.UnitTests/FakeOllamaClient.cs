using BuildingCopilot.Application.Abstractions;

namespace BuildingCopilot.UnitTests;

internal sealed class FakeOllamaClient : IOllamaClient
{
    public string Answer { get; init; } = "Test answer.";
    public string SystemPrompt { get; private set; } = string.Empty;
    public string UserPrompt { get; private set; } = string.Empty;
    public Func<string, float[]> EmbeddingFor { get; init; } = _ => [1f, 0f];

    public Task<string> CompleteAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SystemPrompt = systemPrompt;
        UserPrompt = userPrompt;
        return Task.FromResult(Answer);
    }

    public Task<float[]> CreateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(EmbeddingFor(text));
    }
}