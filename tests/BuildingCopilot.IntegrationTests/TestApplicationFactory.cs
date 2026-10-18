using BuildingCopilot.Application.Abstractions;
using BuildingCopilot.Application.Models;
using BuildingCopilot.Domain.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BuildingCopilot.IntegrationTests;

internal sealed class TestApplicationFactory : WebApplicationFactory<Program>
{
    public Exception? ChatException { get; set; }
    public string ChatAnswer { get; set; } = "Check airflow [S1].";
    public string RagAnswer { get; set; } = "Inspect airflow [S1].";
    public string? LastAdditionalContext { get; private set; }
    public int RagCallCount { get; private set; }
    public bool AuthenticateRequests { get; set; } = true;
    public string[] TestRoles { get; set; } = ["building-user"];

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton(this);
            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "IntegrationTest";
                    options.DefaultChallengeScheme = "IntegrationTest";
                    options.DefaultForbidScheme = "IntegrationTest";
                })
                .AddScheme<AuthenticationSchemeOptions, IntegrationTestAuthHandler>(
                    "IntegrationTest",
                    _ => { });
            services.RemoveAll<IChatService>();
            services.RemoveAll<IRagService>();
            services.RemoveAll<IBuildingTelemetryService>();
            services.AddSingleton<IChatService>(new StubChatService(() => ChatException, () => ChatAnswer));
            services.AddSingleton<IRagService>(new StubRagService(
                () => RagAnswer,
                context => LastAdditionalContext = context,
                () => RagCallCount++));
            services.AddSingleton<IBuildingTelemetryService, StubTelemetryService>();
        });
    }

    private sealed class StubChatService(Func<Exception?> exception, Func<string> answer) : IChatService
    {
        public Task<ChatResponse> AskAsync(string question, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (exception() is { } error)
            {
                throw error;
            }

            return Task.FromResult(new ChatResponse(answer()));
        }
    }

    private sealed class StubRagService(
        Func<string> answer,
        Action<string?> captureContext,
        Action countCall) : IRagService
    {
        public Task<RagResponse> AskAsync(
            string question,
            CancellationToken cancellationToken,
            string? additionalContext = null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            captureContext(additionalContext);
            countCall();
            return Task.FromResult(new RagResponse(answer(),
            [
                new RagSource("S1", "hvac-maintenance-manual", "Inspect airflow and verify the cooling setpoint.", 0.91)
            ]));
        }
    }

    private sealed class StubTelemetryService : IBuildingTelemetryService
    {
        public Task<RoomTelemetry?> GetRoomTelemetryAsync(string roomId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RoomTelemetry? result = roomId.Equals("ROOM-101", StringComparison.OrdinalIgnoreCase)
                ? new RoomTelemetry("ROOM-101", 29.5, true)
                : null;
            return Task.FromResult(result);
        }
    }
}