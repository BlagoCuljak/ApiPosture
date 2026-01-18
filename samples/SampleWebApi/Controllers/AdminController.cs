using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SampleWebApi.Controllers;

/// <summary>
/// Admin controller with sensitive routes.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    // Public admin endpoint - triggers AP001 and AP007 (sensitive keyword)
    [HttpGet("dashboard")]
    public IActionResult Dashboard()
    {
        return Ok("Dashboard");
    }

    // Public delete - triggers AP004 (missing auth on write)
    [HttpDelete("reset")]
    public IActionResult Reset()
    {
        return Ok("Reset");
    }

    // AllowAnonymous on write - triggers AP002
    [HttpPost("export")]
    [AllowAnonymous]
    public IActionResult Export()
    {
        return Ok("Exported");
    }

    // Multiple roles - triggers AP005 (excessive roles)
    [HttpGet("settings")]
    [Authorize(Roles = "Admin,SuperAdmin,Manager,Support")]
    public IActionResult GetSettings()
    {
        return Ok("Settings");
    }

    // Weak role naming - triggers AP006
    [HttpPut("config")]
    [Authorize(Roles = "Admin,User")]
    public IActionResult UpdateConfig()
    {
        return Ok("Config updated");
    }
}
