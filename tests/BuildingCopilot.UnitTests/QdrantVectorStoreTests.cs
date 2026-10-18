using System.Net;
using System.Text;
using System.Text.Json;
using BuildingCopilot.Application.Models;
using BuildingCopilot.Infrastructure.Configuration;
using BuildingCopilot.Infrastructure.Services;
using Microsoft.Extensions.Options;

namespace BuildingCopilot.UnitTests;

public sealed class QdrantVectorStoreTests
{
    [Fact]
    public async Task EnsureCollectionAsync_CreatesCosineCollectionUsingEmbeddingSize()
    {
        var requests = new List<(HttpMethod Method, string Path, string Body)>();
        using var http = CreateClient(async request =>
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync();
            requests.Add((request.Method, request.RequestUri!.PathAndQuery, body));
            return request.Method == HttpMethod.Get
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : new HttpResponseMessage(HttpStatusCode.OK);
        });
        var store = CreateStore(http);

        await store.EnsureCollectionAsync(768, CancellationToken.None);

        Assert.Equal(HttpMethod.Get, requests[0].Method);
        Assert.Equal(HttpMethod.Put, requests[1].Method);
        Assert.Equal("/collections/buildingcopilot-knowledge", requests[1].Path);
        using var bodyJson = JsonDocument.Parse(requests[1].Body);
        Assert.Equal(768, bodyJson.RootElement.GetProperty("vectors").GetProperty("size").GetInt32());
        Assert.Equal("Cosine", bodyJson.RootElement.GetProperty("vectors").GetProperty("distance").GetString());
    }

    [Fact]
    public async Task UpsertAsync_PersistsStableIdSourceAndTextPayload()
    {
        string? requestBody = null;
        using var http = CreateClient(async request =>
        {
            requestBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var store = CreateStore(http);
        var document = new KnowledgeDocument(
            Guid.Parse("8e285ad0-cc44-48d0-b701-f9653d4b9713"),
            "manual.pdf",
            "Inspect the air filter.");

        await store.UpsertAsync(
        [new KnowledgePoint(document, [0.1f, 0.9f])],
        CancellationToken.None);

        using var body = JsonDocument.Parse(requestBody!);
        var point = body.RootElement.GetProperty("points")[0];
        Assert.Equal(document.Id, point.GetProperty("id").GetGuid());
        Assert.Equal("manual.pdf", point.GetProperty("payload").GetProperty("source").GetString());
        Assert.Equal("Inspect the air filter.", point.GetProperty("payload").GetProperty("text").GetString());
        Assert.Equal(0.9f, point.GetProperty("vector")[1].GetSingle());
    }

    [Fact]
    public async Task SearchAsync_MapsQdrantScoreAndPayload_AndSendsApiKey()
    {
        string? apiKey = null;
        using var http = CreateClient(request =>
        {
            apiKey = request.Headers.GetValues("api-key").Single();
            const string json = """
                {"result":{"points":[{"id":"8e285ad0-cc44-48d0-b701-f9653d4b9713","score":0.87,"payload":{"source":"manual.pdf","text":"Inspect the filter."}}]}}
                """;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        });
        http.DefaultRequestHeaders.TryAddWithoutValidation("api-key", "qdrant-test-key");
        var store = CreateStore(http);

        var results = await store.SearchAsync([0.4f, 0.6f], 2, CancellationToken.None);

        Assert.Equal("qdrant-test-key", apiKey);
        var result = Assert.Single(results);
        Assert.Equal("manual.pdf", result.Document.Source);
        Assert.Equal("Inspect the filter.", result.Document.Text);
        Assert.Equal(0.87, result.Score);
    }

    private static HttpClient CreateClient(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> send) =>
        new(new StubHandler(send)) { BaseAddress = new Uri("http://qdrant.test:6333/") };

    private static QdrantVectorStore CreateStore(HttpClient httpClient) =>
        new(httpClient, Options.Create(new QdrantOptions
        {
            BaseUrl = "http://qdrant.test:6333/",
            CollectionName = "buildingcopilot-knowledge"
        }));

    private sealed class StubHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => send(request);
    }
}