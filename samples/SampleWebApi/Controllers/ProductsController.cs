using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SampleWebApi.Controllers;

/// <summary>
/// Controller with various authorization patterns for testing.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]  // Controller-level authorization
public class ProductsController : ControllerBase
{
    // Inherits Authorize from controller - should be Authenticated
    [HttpGet]
    public IActionResult GetAll()
    {
        return Ok(new[] { "Product 1", "Product 2" });
    }

    // Explicit role - should be RoleRestricted
    [HttpGet("{id}")]
    [Authorize(Roles = "ProductViewer")]
    public IActionResult GetById(int id)
    {
        return Ok($"Product {id}");
    }

    // Policy-based - should be PolicyRestricted
    [HttpPost]
    [Authorize(Policy = "CanCreateProducts")]
    public IActionResult Create()
    {
        return Created("/api/products/1", null);
    }

    // AllowAnonymous overrides controller Authorize - triggers AP003
    [HttpGet("public")]
    [AllowAnonymous]
    public IActionResult GetPublicProducts()
    {
        return Ok(new[] { "Public Product" });
    }
}
