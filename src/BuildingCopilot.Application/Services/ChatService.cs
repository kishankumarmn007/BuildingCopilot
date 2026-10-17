using BuildingCopilot.Application.Abstractions;
using BuildingCopilot.Application.Models;

namespace BuildingCopilot.Application.Services;

public sealed class ChatService(IOllamaClient ollamaClient) : IChatService
{
    private const string SystemPrompt = """
        You are a concise assistant for building operations and facilities teams.
        Give practical, clearly qualified guidance. Do not claim an observation
        or diagnosis that is not supported by the provided information.
        """;

    public async Task<ChatResponse> AskAsync(
        string question,
        CancellationToken cancellationToken)
    {
        var answer = await ollamaClient.CompleteAsync(
            SystemPrompt,
            question,
            cancellationToken);

        return new ChatResponse(answer);
    }
}