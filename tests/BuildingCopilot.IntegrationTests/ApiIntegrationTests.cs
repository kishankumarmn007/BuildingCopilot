using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace BuildingCopilot.IntegrationTests;

public sealed class ApiIntegrationTests
{
    [Fact]
    public async Task ProtectedRestAndMcpRoutes_RejectAnonymousRequests()
    {
        using var factory = new TestApplicationFactory { AuthenticateRequests = false };
        using var client = factory.CreateClient();

        var telemetry = await client.GetAsync("/api/rooms/ROOM-101/telemetry");
        var rag = await client.PostAsJsonAsync("/api/rag/ask", new { question = "hello" });
        using var mcpRequest = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = JsonContent.Create(new { jsonrpc = "2.0", id = 1, method = "tools/list", @params = new { } })
        };
        mcpRequest.Headers.Accept.ParseAdd("application/json");
        mcpRequest.Headers.Accept.ParseAdd("text/event-stream");
        var mcp = await client.SendAsync(mcpRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, telemetry.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, rag.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, mcp.StatusCode);
    }

    [Fact]
    public async Task ProtectedApi_RejectsAuthenticatedUsersWithoutBuildingRole()
    {
        using var factory = new TestApplicationFactory { TestRoles = [] };
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/rooms/ROOM-101/telemetry");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Health_ReturnsSuccess()
    {
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Building copilot API is running", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Telemetry_KnownRoomReturnsData_UnknownRoomReturnsProblemDetails()
    {
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var known = await client.GetFromJsonAsync<JsonElement>("/api/rooms/ROOM-101/telemetry");
        var missingResponse = await client.GetAsync("/api/rooms/ROOM-999/telemetry");
        var missing = await missingResponse.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(29.5, known.GetProperty("temperatureC").GetDouble());
        Assert.True(known.GetProperty("hvacRunning").GetBoolean());
        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);
        Assert.Equal("Room not found.", missing?.Title);
    }

    [Fact]
    public async Task Chat_EmptyQuestionReturnsBadRequestProblemDetails()
    {
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/chat", new { question = " " });
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(StatusCodes.Status400BadRequest, problem?.Status);
    }

    [Fact]
    public async Task Chat_UnreachableUpstreamMapsToServiceUnavailable()
    {
        using var factory = new TestApplicationFactory
        {
            ChatException = new HttpRequestException("Internal transport details.")
        };
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/chat", new { question = "hello" });
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Language model service unavailable", problem?.Title);
        Assert.DoesNotContain("Internal transport details", problem?.Detail);
    }

    [Fact]
    public async Task Chat_UpstreamHttpErrorMapsToBadGateway()
    {
        using var factory = new TestApplicationFactory
        {
            ChatException = new HttpRequestException("Upstream detail.", null, HttpStatusCode.NotFound)
        };
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/chat", new { question = "hello" });
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("Language model request failed", problem?.Title);
    }

    [Fact]
    public async Task Chat_PollyTimeoutMapsToGatewayTimeout()
    {
        using var factory = new TestApplicationFactory
        {
            ChatException = new TimeoutRejectedException("Attempt timed out.")
        };
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/chat", new { question = "hello" });
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        Assert.Equal("Language model request timed out", problem?.Title);
    }

    [Fact]
    public async Task Chat_OpenCircuitMapsToServiceUnavailable()
    {
        using var factory = new TestApplicationFactory
        {
            ChatException = new BrokenCircuitException("Circuit is open.")
        };
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/chat", new { question = "hello" });
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Language model service temporarily unavailable", problem?.Title);
    }

    [Fact]
    public async Task Rag_ReturnsCitedSources()
    {
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/rag/ask", new { question = "What should I inspect?" });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Inspect airflow [S1].", body.GetProperty("answer").GetString());
        Assert.Equal("S1", body.GetProperty("sources")[0].GetProperty("citationId").GetString());
    }

    [Fact]
    public async Task Diagnosis_IncludesTelemetryAsRagContext()
    {
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/rooms/ROOM-101/diagnosis",
            new { question = "Why is the room warm?" });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("ROOM-101", factory.LastAdditionalContext);
        Assert.Contains("29.5 C", factory.LastAdditionalContext);
        Assert.True(body.GetProperty("room").GetProperty("hvacRunning").GetBoolean());
        Assert.Equal(1, factory.RagCallCount);
    }

    [Fact]
    public async Task Diagnosis_UnknownRoomDoesNotCallRag()
    {
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/rooms/ROOM-999/diagnosis",
            new { question = "Why is the room warm?" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, factory.RagCallCount);
    }
}