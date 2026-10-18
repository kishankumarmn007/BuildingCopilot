using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace BuildingCopilot.Api.Controllers;

[ApiController]
[Route("")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType<string>(StatusCodes.Status200OK)]
    public IActionResult Get() => Ok("Building copilot API is running");
}