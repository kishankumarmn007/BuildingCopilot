using System.Net;
using System.Text;
using BuildingCopilot.Application.Abstractions;
using BuildingCopilot.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingCopilot.IntegrationTests;

public sealed class OllamaResilienceIntegrationTests
{
    [Fact]
    public async Task TransientServerErrors_AreRetriedWithTheConfiguredPipeline()
    {
        var attempts = 0;
        var services = new ServiceCollection();
        services.AddLogging();
        services.ConfigureHttpClientDefaults(builder =>
            builder.ConfigurePrimaryHttpMessageHandler(() => new StubHandler((_, _) =>
            {
                var attempt = Interlocked.Increment(ref attempts);
                var status = attempt < 3 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK;
                var response = new HttpResponseMessage(status);
                if (status == HttpStatusCode.OK)
                {
                    response.Content = new StringContent(
                        "{\"message\":{\"content\":\"recovered\"}}",
                        Encoding.UTF8,
                        "application/json");
                }

                return Task.FromResult(response);
            })));
        services.AddInfrastructure(CreateTestConfiguration());

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IOllamaClient>();

        var answer = await client.CompleteAsync("system", "question", CancellationToken.None);

        Assert.Equal("recovered", answer);
        Assert.Equal(3, attempts);
    }

    private static IConfiguration CreateTestConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ollama:BaseUrl"] = "http://localhost:11434/",
                ["Ollama:ChatModel"] = "test-model",
                ["Ollama:EmbeddingModel"] = "test-embedding",
                ["Ollama:Resilience:MaxRetryAttempts"] = "2",
                ["Ollama:Resilience:RetryDelayMilliseconds"] = "1",
                ["Ollama:Resilience:AttemptTimeoutSeconds"] = "1",
                ["Ollama:Resilience:TotalRequestTimeoutSeconds"] = "5",
                ["Ollama:Resilience:CircuitBreakerMinimumThroughput"] = "4",
                ["Ollama:Resilience:CircuitBreakerBreakSeconds"] = "1"
            })
            .Build();

    private sealed class StubHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => send(request, cancellationToken);
    }
}