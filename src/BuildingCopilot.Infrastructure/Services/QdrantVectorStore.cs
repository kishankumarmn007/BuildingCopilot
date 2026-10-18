using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BuildingCopilot.Application.Abstractions;
using BuildingCopilot.Application.Models;
using BuildingCopilot.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace BuildingCopilot.Infrastructure.Services;

public sealed class QdrantVectorStore(
    HttpClient httpClient,
    IOptions<QdrantOptions> options) : IVectorStore
{
    private static readonly JsonSerializerOptions QdrantJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _collection = Uri.EscapeDataString(options.Value.CollectionName);

    public async Task EnsureCollectionAsync(
        int vectorSize,
        CancellationToken cancellationToken)
    {
        using var existing = await httpClient.GetAsync(
            $"collections/{_collection}",
            cancellationToken);

        if (existing.IsSuccessStatusCode)
        {
            return;
        }

        if (existing.StatusCode != HttpStatusCode.NotFound)
        {
            existing.EnsureSuccessStatusCode();
        }

        using var create = await httpClient.PutAsJsonAsync(
            $"collections/{_collection}",
            new { vectors = new { size = vectorSize, distance = "Cosine" } },
            QdrantJson,
            cancellationToken);

        if (create.StatusCode == HttpStatusCode.Conflict)
        {
            return;
        }

        create.EnsureSuccessStatusCode();
    }

    public async Task<bool> HasDocumentsAsync(CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(
            $"collections/{_collection}/points/scroll",
            new
            {
                limit = 1,
                with_payload = false,
                with_vector = false
            },
            QdrantJson,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        using var json = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);

        return json.RootElement
            .GetProperty("result")
            .GetProperty("points")
            .GetArrayLength() > 0;
    }

    public async Task UpsertAsync(
        IReadOnlyList<KnowledgePoint> points,
        CancellationToken cancellationToken)
    {
        if (points.Count == 0)
        {
            return;
        }

        using var response = await httpClient.PutAsJsonAsync(
            $"collections/{_collection}/points?wait=true",
            new
            {
                points = points.Select(point => new
                {
                    id = point.Document.Id,
                    vector = point.Vector,
                    payload = new
                    {
                        source = point.Document.Source,
                        text = point.Document.Text
                    }
                })
            },
            QdrantJson,
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<KnowledgeMatch>> SearchAsync(
        float[] vector,
        int limit,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(
            $"collections/{_collection}/points/query",
            new
            {
                query = vector,
                limit,
                with_payload = new[] { "source", "text" }
            },
            QdrantJson,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        using var json = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);

        return json.RootElement
            .GetProperty("result")
            .GetProperty("points")
            .EnumerateArray()
            .Select(point =>
            {
                var payload = point.GetProperty("payload");
                return new KnowledgeMatch(
                    new KnowledgeDocument(
                        Guid.Parse(point.GetProperty("id").GetString()!),
                        payload.GetProperty("source").GetString()!,
                        payload.GetProperty("text").GetString()!),
                    point.GetProperty("score").GetDouble());
            })
            .ToArray();
    }
}