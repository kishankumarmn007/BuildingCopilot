namespace BuildingCopilot.Api.Contracts;

/// <summary>Observed telemetry for a building room.</summary>
public sealed record RoomTelemetryResponse(
    string RoomId,
    double TemperatureC,
    bool HvacRunning);