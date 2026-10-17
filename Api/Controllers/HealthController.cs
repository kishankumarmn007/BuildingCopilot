using Microsoft.AspNetCore.Mvc;

namespace BuildingCopilot.Api.Controllers;

[ApiController]
[Route("")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<string>(StatusCodes.Status200OK)]
    public IActionResult Get() => Ok("Building copilot API is running");
}