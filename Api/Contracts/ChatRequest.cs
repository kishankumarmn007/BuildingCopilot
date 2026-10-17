namespace BuildingCopilot.Api.Contracts;

/// <summary>Question submitted to a chat or building-diagnosis endpoint.</summary>
public sealed record ChatRequest
{
    /// <summary>Natural-language question to answer.</summary>
    public required string Question { get; init; }
}