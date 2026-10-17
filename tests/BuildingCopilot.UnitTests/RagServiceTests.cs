using BuildingCopilot.Application.Services;

namespace BuildingCopilot.UnitTests;

public sealed class RagServiceTests
{
    [Fact]
    public async Task AskAsync_ReturnsTwoRankedSourcesAndSendsCitationLabelsToModel()
    {
        var ollama = new FakeOllamaClient
        {
            Answer = "Inspect airflow [S1] and the setpoint [S2].",
            EmbeddingFor = text => text switch
            {
                "airflow issue" => [1f, 0f],
                var value when value.Contains("inspect airflow", StringComparison.OrdinalIgnoreCase) => [1f, 0f],
                var value when value.Contains("temperature remains above", StringComparison.OrdinalIgnoreCase) => [0.8f, 0.2f],
                _ => [0f, 1f]
            }
        };
        var service = new RagService(ollama);

        var response = await service.AskAsync(
            "airflow issue",
            CancellationToken.None,
            "Room ROOM-101 is at 29.5 C.");

        Assert.Equal(2, response.Sources.Count);
        Assert.Equal(["S1", "S2"], response.Sources.Select(source => source.CitationId));
        Assert.Contains("[S1]", ollama.UserPrompt);
        Assert.Contains("[S2]", ollama.UserPrompt);
        Assert.Contains("ROOM-101 is at 29.5 C", ollama.UserPrompt);
        Assert.Equal("Inspect airflow [S1] and the setpoint [S2].", response.Answer);
    }

    [Fact]
    public async Task AskAsync_RejectsEmbeddingDimensionMismatch()
    {
        var ollama = new FakeOllamaClient
        {
            EmbeddingFor = text => text == "question" ? [1f, 0f] : [1f]
        };
        var service = new RagService(ollama);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.AskAsync("question", CancellationToken.None));
    }

    [Fact]
    public async Task AskAsync_AddsCitationFooterWhenModelOmitsSourceLabels()
    {
        var ollama = new FakeOllamaClient
        {
            Answer = "Inspect airflow and the cooling setpoint.",
            EmbeddingFor = text => text == "question" ? [1f, 0f] : [1f, 0f]
        };
        var service = new RagService(ollama);

        var response = await service.AskAsync("question", CancellationToken.None);

        Assert.Contains("Sources: [S1], [S2]", response.Answer);
    }

    [Fact]
    public async Task AskAsync_ForwardsCancellationToEmbeddingProvider()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var ollama = new FakeOllamaClient
        {
            EmbeddingFor = _ => throw new OperationCanceledException(cancellation.Token)
        };
        var service = new RagService(ollama);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.AskAsync("question", cancellation.Token));
    }
}