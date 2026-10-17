using System.Net.Http.Json;
using System.Text.Json;

namespace BuildingCopilot.IntegrationTests;

public sealed class McpIntegrationTests
{
    [Fact]
    public async Task StreamableHttp_InitializesListsToolsAndInvokesRoomTelemetry()
    {
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        using var initialize = await SendAsync(client, "initialize", 1, new
        {
            protocolVersion = "2025-11-25",
            capabilities = new { },
            clientInfo = new { name = "integration-tests", version = "1.0.0" }
        });
        Assert.True(initialize.RootElement.TryGetProperty("result", out var initializeResult));
        Assert.True(initializeResult.TryGetProperty("capabilities", out _));

        using var initialized = await SendAsync(client, "notifications/initialized", null, null);
        Assert.Empty(initialized.RootElement.EnumerateObject());

        using var toolList = await SendAsync(client, "tools/list", 2, new { });
        var tools = toolList.RootElement.GetProperty("result").GetProperty("tools");
        var telemetryTool = tools.EnumerateArray().Single(tool =>
            tool.GetProperty("description").GetString()!.Contains("current telemetry", StringComparison.OrdinalIgnoreCase));
        var toolName = telemetryTool.GetProperty("name").GetString();

        using var toolCall = await SendAsync(client, "tools/call", 3, new
        {
            name = toolName,
            arguments = new { roomId = "ROOM-101" }
        });
        var toolResult = toolCall.RootElement.GetProperty("result");
        Assert.False(toolResult.TryGetProperty("isError", out var isError) && isError.GetBoolean());
        var text = toolResult.GetProperty("content")[0].GetProperty("text").GetString();
        using var telemetry = JsonDocument.Parse(text!);
        Assert.Equal("ROOM-101", telemetry.RootElement.GetProperty("roomId").GetString());
        Assert.Equal(29.5, telemetry.RootElement.GetProperty("temperatureC").GetDouble());
    }

    private static async Task<JsonDocument> SendAsync(
        HttpClient client,
        string method,
        int? id,
        object? parameters)
    {
        var jsonRpc = id is null
            ? new Dictionary<string, object?>
            {
                ["jsonrpc"] = "2.0",
                ["method"] = method
            }
            : new Dictionary<string, object?>
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id,
                ["method"] = method,
                ["params"] = parameters
            };
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = JsonContent.Create(jsonRpc)
        };
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        request.Headers.TryAddWithoutValidation("MCP-Protocol-Version", "2025-11-25");

        using var response = await client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"MCP {method} failed ({(int)response.StatusCode}): {content}");
        if (response.StatusCode == System.Net.HttpStatusCode.Accepted)
        {
            return JsonDocument.Parse("{}");
        }

        var json = content
            .Split('\n')
            .FirstOrDefault(line => line.StartsWith("data:", StringComparison.Ordinal))?
            .Substring("data:".Length)
            .Trim();
        return JsonDocument.Parse(json ?? content);
    }
}