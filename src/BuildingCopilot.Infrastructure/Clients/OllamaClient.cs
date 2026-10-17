using System.Net.Http.Json;
using System.Text.Json;
using BuildingCopilot.Application.Abstractions;
using BuildingCopilot.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace BuildingCopilot.Infrastructure.Clients;

public sealed class OllamaClient(
    HttpClient httpClient,
    IOptions<OllamaOptions> options) : IOllamaClient
{
    private readonly OllamaOptions _options = options.Value;

    public async Task<string> CompleteAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "api/chat",
            new
            {
                model = _options.ChatModel,
                stream = false,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                }
            },
            cancellationToken);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ChatApiResponse>(
            cancellationToken: cancellationToken);

        return result?.Message?.Content
            ?? throw new InvalidOperationException("Ollama returned no chat content.");
    }

    public async Task<float[]> CreateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "api/embed",
            new
            {
                model = _options.EmbeddingModel,
                input = text
            },
            cancellationToken);

        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(
            stream,
            cancellationToken: cancellationToken);

        return json.RootElement
            .GetProperty("embeddings")[0]
            .EnumerateArray()
            .Select(value => value.GetSingle())
            .ToArray();
    }

    private sealed record ChatApiResponse(ChatMessage? Message);
    private sealed record ChatMessage(string? Content);
}