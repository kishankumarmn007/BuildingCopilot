using BuildingCopilot.Domain.Entities;

namespace BuildingCopilot.Application.Abstractions;

public interface IBuildingTelemetryService
{
    Task<RoomTelemetry?> GetRoomTelemetryAsync(
        string roomId,
        CancellationToken cancellationToken);
}