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
        services.AddSingleton<IBuildingTelemetryService, BuildingTelemetryService>();

        return services;
    }
}