using System.ComponentModel;
using System.Text.Json;
using BuildingCopilot.Application.Abstractions;
using ModelContextProtocol.Server;

namespace BuildingCopilot.Api.Mcp;

[McpServerToolType]
public sealed class BuildingCopilotMcpTools(
    IBuildingTelemetryService telemetryService,
    IRagService ragService)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [McpServerTool, Description("Returns current telemetry for a building room, including temperature and HVAC state.")]
    public async Task<string> GetRoomTelemetry(
        [Description("Room identifier, for example ROOM-101.")] string roomId,
        CancellationToken cancellationToken)
    {
        var telemetry = await telemetryService.GetRoomTelemetryAsync(roomId, cancellationToken);
        return telemetry is null
            ? JsonSerializer.Serialize(new { error = "Room not found", roomId }, JsonOptions)
            : JsonSerializer.Serialize(telemetry, JsonOptions);
    }

    [McpServerTool, Description("Answers a building-maintenance question using relevant manual context and includes source citations.")]
    public async Task<string> AskBuildingManual(
        [Description("The maintenance or building-operations question.")] string question,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            throw new ArgumentException("Question is required.", nameof(question));
        }

        var response = await ragService.AskAsync(question, cancellationToken);
        return JsonSerializer.Serialize(response, JsonOptions);
    }
}