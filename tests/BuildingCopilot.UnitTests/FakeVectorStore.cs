using BuildingCopilot.Application.Abstractions;
using BuildingCopilot.Application.Models;

namespace BuildingCopilot.UnitTests;

internal sealed class FakeVectorStore : IVectorStore
{
    private readonly List<KnowledgePoint> _points = [];

    public int CollectionVectorSize { get; private set; }
    public int UpsertCallCount { get; private set; }
    public float[]? SearchedVector { get; private set; }
    public int SearchLimit { get; private set; }
    public IReadOnlyList<KnowledgePoint> Points => _points;

    public IReadOnlyList<KnowledgeMatch> SearchResults { get; set; } = [];

    public Task EnsureCollectionAsync(int vectorSize, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CollectionVectorSize = vectorSize;
        return Task.CompletedTask;
    }

    public Task<bool> HasDocumentsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_points.Count > 0);
    }

    public Task UpsertAsync(IReadOnlyList<KnowledgePoint> points, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        UpsertCallCount++;
        _points.AddRange(points);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<KnowledgeMatch>> SearchAsync(
        float[] vector,
        int limit,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SearchedVector = vector;
        SearchLimit = limit;
        var matches = SearchResults.Count > 0
            ? SearchResults
            : _points.Select((point, index) => new KnowledgeMatch(point.Document, 1 - index * 0.1)).ToArray();
        return Task.FromResult<IReadOnlyList<KnowledgeMatch>>(matches.Take(limit).ToArray());
    }
}