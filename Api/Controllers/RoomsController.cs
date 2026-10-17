using BuildingCopilot.Api.Contracts;
using BuildingCopilot.Application.Abstractions;
using BuildingCopilot.Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace BuildingCopilot.Api.Controllers;

[ApiController]
[Route("api/rooms")]
public sealed class RoomsController(
    IBuildingTelemetryService telemetryService,
    IRagService ragService) : ControllerBase
{
    [HttpGet("{roomId}/telemetry")]
    [ProducesResponseType<RoomTelemetryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoomTelemetryResponse>> GetTelemetryAsync(
        string roomId,
        CancellationToken cancellationToken)
    {
        var telemetry = await telemetryService.GetRoomTelemetryAsync(roomId, cancellationToken);
        if (telemetry is null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Room not found."
            });
        }

        return Ok(new RoomTelemetryResponse(
            telemetry.RoomId,
            telemetry.TemperatureC,
            telemetry.HvacRunning));
    }

    [HttpPost("{roomId}/diagnosis")]
    [ProducesResponseType<RoomDiagnosisResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoomDiagnosisResponse>> DiagnoseAsync(
        string roomId,
        [FromBody] ChatRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [nameof(request.Question)] = ["Question is required."]
            })
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Question is required."
            });
        }

        var telemetry = await telemetryService.GetRoomTelemetryAsync(roomId, cancellationToken);
        if (telemetry is null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Room not found."
            });
        }

        var observedData = $"Room: {telemetry.RoomId}; Temperature: {telemetry.TemperatureC} C; HVAC running: {telemetry.HvacRunning}.";
        var analysis = await ragService.AskAsync(
            request.Question,
            cancellationToken,
            observedData);
        var response = new RoomDiagnosisResponse(
            new RoomTelemetryResponse(
                telemetry.RoomId,
                telemetry.TemperatureC,
                telemetry.HvacRunning),
            analysis);

        return Ok(response);
    }
}