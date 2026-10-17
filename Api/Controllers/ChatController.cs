using BuildingCopilot.Api.Contracts;
using BuildingCopilot.Application.Abstractions;
using BuildingCopilot.Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace BuildingCopilot.Api.Controllers;

[ApiController]
[Route("api/chat")]
public sealed class ChatController(IChatService chatService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<ChatResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ChatResponse>> AskAsync(
        [FromBody] ChatRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [nameof(request.Question)] = ["Question is required."]
            })
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Question is required."
            });
        }

        var response = await chatService.AskAsync(request.Question, cancellationToken);
        return Ok(response);
    }
}