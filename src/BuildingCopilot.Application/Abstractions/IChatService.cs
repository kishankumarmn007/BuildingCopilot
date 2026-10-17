using BuildingCopilot.Application.Models;

namespace BuildingCopilot.Application.Abstractions;

public interface IChatService
{
    Task<ChatResponse> AskAsync(
        string question,
        CancellationToken cancellationToken);
}