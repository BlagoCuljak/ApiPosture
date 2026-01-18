using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SampleWebApi.Controllers;

/// <summary>
/// Controller with explicit public endpoints.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class PublicController : ControllerBase
{
    // Properly marked as anonymous - no findings
    [HttpGet("health")]
    [AllowAnonymous]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy" });
    }

    // Properly marked as anonymous - no AP001, but triggers AP002 (write)
    [HttpPost("feedback")]
    [AllowAnonymous]
    public IActionResult SubmitFeedback()
    {
        return Ok("Thanks for your feedback");
    }

    // No explicit intent - triggers AP001
    [HttpGet("version")]
    public IActionResult GetVersion()
    {
        return Ok(new { version = "1.0.0" });
    }
}
