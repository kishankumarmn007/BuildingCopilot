using BuildingCopilot.Application.Services;
using BuildingCopilot.Application.Models;

namespace BuildingCopilot.UnitTests;

public sealed class RagServiceTests
{
    [Fact]
    public async Task AskAsync_ReturnsTwoRankedSourcesAndSendsCitationLabelsToModel()
    {
        var ollama = new FakeOllamaClient
        {
            Answer = "Inspect airflow [S1] and the setpoint [S2].",
            EmbeddingFor = _ => [1f, 0f]
        };
        var vectorStore = new FakeVectorStore
        {
            SearchResults =
            [
                new KnowledgeMatch(new KnowledgeDocument(Guid.NewGuid(), "hvac-guide", "Inspect airflow."), 0.91),
                new KnowledgeMatch(new KnowledgeDocument(Guid.NewGuid(), "setpoint-guide", "Verify the cooling setpoint."), 0.82)
            ]
        };
        var service = new RagService(ollama, vectorStore);

        var response = await service.AskAsync(
            "airflow issue",
            CancellationToken.None,
            "Room ROOM-101 is at 29.5 C.");

        Assert.Equal(2, response.Sources.Count);
        Assert.Equal(["S1", "S2"], response.Sources.Select(source => source.CitationId));
        Assert.Equal(["hvac-guide", "setpoint-guide"], response.Sources.Select(source => source.Source));
        Assert.Contains("[S1]", ollama.UserPrompt);
        Assert.Contains("[S2]", ollama.UserPrompt);
        Assert.Contains("ROOM-101 is at 29.5 C", ollama.UserPrompt);
        Assert.Equal(3, vectorStore.Points.Count);
        Assert.Equal(2, vectorStore.CollectionVectorSize);
        Assert.Equal(3, vectorStore.SearchLimit);
        Assert.Equal([1f, 0f], vectorStore.SearchedVector!);
        Assert.Equal("Inspect airflow [S1] and the setpoint [S2].", response.Answer);
    }

    [Fact]
    public async Task AskAsync_DoesNotSeedWhenVectorCollectionAlreadyContainsDocuments()
    {
        var vectorStore = new FakeVectorStore();
        await vectorStore.UpsertAsync(
        [
            new KnowledgePoint(
                new KnowledgeDocument(Guid.NewGuid(), "existing", "Existing manual content."),
                [1f, 0f])
        ],
        CancellationToken.None);
        var service = new RagService(new FakeOllamaClient(), vectorStore);

        await service.AskAsync("question", CancellationToken.None);

        Assert.Equal(1, vectorStore.UpsertCallCount);
        Assert.Single(vectorStore.Points);
    }

    [Fact]
    public async Task AskAsync_AddsCitationFooterWhenModelOmitsSourceLabels()
    {
        var ollama = new FakeOllamaClient
        {
            Answer = "Inspect airflow and the cooling setpoint.",
            EmbeddingFor = text => text == "question" ? [1f, 0f] : [1f, 0f]
        };
        var service = new RagService(ollama, new FakeVectorStore());

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
        var service = new RagService(ollama, new FakeVectorStore());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.AskAsync("question", cancellation.Token));
    }
}