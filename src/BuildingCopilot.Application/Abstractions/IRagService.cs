using BuildingCopilot.Application.Models;

namespace BuildingCopilot.Application.Abstractions;

public interface IRagService
{
    Task<RagResponse> AskAsync(
        string question,
        CancellationToken cancellationToken,
        string? additionalContext = null);
}