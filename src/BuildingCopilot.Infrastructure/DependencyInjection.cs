using BuildingCopilot.Application.Abstractions;
using BuildingCopilot.Infrastructure.Clients;
using BuildingCopilot.Infrastructure.Configuration;
using BuildingCopilot.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;

namespace BuildingCopilot.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<OllamaOptions>(
            configuration.GetSection(OllamaOptions.SectionName));
        var ollamaClient = services.AddHttpClient<IOllamaClient, OllamaClient>((provider, client) =>
        {
            var options = provider
                .GetRequiredService<Microsoft.Extensions.Options.IOptions<OllamaOptions>>()
                .Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = Timeout.InfiniteTimeSpan;
        });
        ollamaClient.AddStandardResilienceHandler(options =>
        {
            var resilience = configuration.GetSection("Ollama:Resilience");
            options.Retry.MaxRetryAttempts = resilience.GetValue("MaxRetryAttempts", 2);
            options.Retry.Delay = TimeSpan.FromMilliseconds(
                resilience.GetValue("RetryDelayMilliseconds", 300));
            options.Retry.BackoffType = DelayBackoffType.Exponential;
            options.Retry.UseJitter = true;
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(
                resilience.GetValue("AttemptTimeoutSeconds", 75));
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(
                resilience.GetValue("TotalRequestTimeoutSeconds", 180));
            options.CircuitBreaker.FailureRatio = 0.5;
            options.CircuitBreaker.MinimumThroughput = resilience.GetValue(
                "CircuitBreakerMinimumThroughput", 4);
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(180);
            options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(
                resilience.GetValue("CircuitBreakerBreakSeconds", 20));
        });

        services.Configure<QdrantOptions>(
            configuration.GetSection(QdrantOptions.SectionName));
        var qdrantClient = services.AddHttpClient<IVectorStore, QdrantVectorStore>((provider, client) =>
        {
            var options = provider
                .GetRequiredService<Microsoft.Extensions.Options.IOptions<QdrantOptions>>()
                .Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = Timeout.InfiniteTimeSpan;
            if (!string.IsNullOrWhiteSpace(options.ApiKey))
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation("api-key", options.ApiKey);
            }
        });
        qdrantClient.AddStandardResilienceHandler(options =>
        {
            options.Retry.MaxRetryAttempts = 2;
            options.Retry.BackoffType = DelayBackoffType.Exponential;
            options.Retry.UseJitter = true;
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
            options.CircuitBreaker.FailureRatio = 0.5;
            options.CircuitBreaker.MinimumThroughput = 4;
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
            options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);
        });
        services.AddSingleton<IBuildingTelemetryService, BuildingTelemetryService>();

        return services;
    }
}