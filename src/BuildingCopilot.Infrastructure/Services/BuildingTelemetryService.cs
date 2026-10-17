using BuildingCopilot.Application.Abstractions;
using BuildingCopilot.Domain.Entities;

namespace BuildingCopilot.Infrastructure.Services;

public sealed class BuildingTelemetryService : IBuildingTelemetryService
{
    public Task<RoomTelemetry?> GetRoomTelemetryAsync(
        string roomId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        RoomTelemetry? telemetry = roomId.Equals(
            "ROOM-101",
            StringComparison.OrdinalIgnoreCase)
            ? new RoomTelemetry("ROOM-101", 29.5, true)
            : null;

        return Task.FromResult(telemetry);
    }
}