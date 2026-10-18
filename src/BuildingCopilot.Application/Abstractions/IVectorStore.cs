using BuildingCopilot.Application.Models;

namespace BuildingCopilot.Application.Abstractions;

public interface IVectorStore
{
    Task EnsureCollectionAsync(int vectorSize, CancellationToken cancellationToken);

    Task<bool> HasDocumentsAsync(CancellationToken cancellationToken);

    Task UpsertAsync(
        IReadOnlyList<KnowledgePoint> points,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<KnowledgeMatch>> SearchAsync(
        float[] vector,
        int limit,
        CancellationToken cancellationToken);
}