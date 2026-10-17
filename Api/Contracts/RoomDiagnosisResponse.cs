using BuildingCopilot.Application.Models;

namespace BuildingCopilot.Api.Contracts;

/// <summary>Room telemetry and a retrieval-grounded diagnosis.</summary>
public sealed record RoomDiagnosisResponse(
    RoomTelemetryResponse Room,
    RagResponse Analysis);