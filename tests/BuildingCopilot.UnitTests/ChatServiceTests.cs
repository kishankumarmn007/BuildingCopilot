using BuildingCopilot.Application.Services;

namespace BuildingCopilot.UnitTests;

public sealed class ChatServiceTests
{
    [Fact]
    public async Task AskAsync_UsesBuildingSystemPromptAndReturnsModelAnswer()
    {
        var ollama = new FakeOllamaClient { Answer = "Check the airflow." };
        var service = new ChatService(ollama);

        var response = await service.AskAsync(
            "Why is the room hot?",
            CancellationToken.None);

        Assert.Equal("Check the airflow.", response.Answer);
        Assert.Contains("building operations", ollama.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Why is the room hot?", ollama.UserPrompt);
    }
}