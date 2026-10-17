using BuildingCopilot.Api.Contracts;
using BuildingCopilot.Application.Abstractions;
using BuildingCopilot.Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace BuildingCopilot.Api.Controllers;

[ApiController]
[Route("api/rag")]
public sealed class RagController(IRagService ragService) : ControllerBase
{
    [HttpPost("ask")]
    [ProducesResponseType<RagResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RagResponse>> AskAsync(
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

        var response = await ragService.AskAsync(request.Question, cancellationToken);
        return Ok(response);
    }
}