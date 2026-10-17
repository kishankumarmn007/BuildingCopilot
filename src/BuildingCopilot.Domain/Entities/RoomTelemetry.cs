namespace BuildingCopilot.Domain.Entities;

public sealed record RoomTelemetry(
    string RoomId,
    double TemperatureC,
    bool HvacRunning);