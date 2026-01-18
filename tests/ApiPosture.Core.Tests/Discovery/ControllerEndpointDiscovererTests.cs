using ApiPosture.Core.Analysis;
using ApiPosture.Core.Discovery;
using ApiPosture.Core.Models;
using FluentAssertions;
using HttpMethod = ApiPosture.Core.Models.HttpMethod;

namespace ApiPosture.Core.Tests.Discovery;

public class ControllerEndpointDiscovererTests
{
    private readonly ControllerEndpointDiscoverer _discoverer = new();

    [Fact]
    public void Discover_ControllerWithHttpGet_ReturnsEndpoint()
    {
        var code = """
            using Microsoft.AspNetCore.Mvc;

            [ApiController]
            [Route("api/[controller]")]
            public class ProductsController : ControllerBase
            {
                [HttpGet]
                public IActionResult GetAll() => Ok();
            }
            """;

        var tree = SourceFileLoader.ParseText(code);
        var endpoints = _discoverer.Discover(tree).ToList();

        endpoints.Should().HaveCount(1);
        endpoints[0].Route.Should().Be("/api/Products/GetAll");
        endpoints[0].Methods.Should().Be(HttpMethod.Get);
        endpoints[0].Type.Should().Be(EndpointType.Controller);
        endpoints[0].ControllerName.Should().Be("Products");
        endpoints[0].ActionName.Should().Be("GetAll");
    }

    [Fact]
    public void Discover_ControllerWithMultipleActions_ReturnsAllEndpoints()
    {
        var code = """
            using Microsoft.AspNetCore.Mvc;

            [ApiController]
            [Route("api/[controller]")]
            public class OrdersController : ControllerBase
            {
                [HttpGet]
                public IActionResult GetAll() => Ok();

                [HttpGet("{id}")]
                public IActionResult GetById(int id) => Ok();

                [HttpPost]
                public IActionResult Create() => Created("", null);

                [HttpPut("{id}")]
                public IActionResult Update(int id) => Ok();

                [HttpDelete("{id}")]
                public IActionResult Delete(int id) => NoContent();
            }
            """;

        var tree = SourceFileLoader.ParseText(code);
        var endpoints = _discoverer.Discover(tree).ToList();

        endpoints.Should().HaveCount(5);
        endpoints.Should().Contain(e => e.Methods == HttpMethod.Get && e.ActionName == "GetAll");
        endpoints.Should().Contain(e => e.Methods == HttpMethod.Get && e.ActionName == "GetById");
        endpoints.Should().Contain(e => e.Methods == HttpMethod.Post && e.ActionName == "Create");
        endpoints.Should().Contain(e => e.Methods == HttpMethod.Put && e.ActionName == "Update");
        endpoints.Should().Contain(e => e.Methods == HttpMethod.Delete && e.ActionName == "Delete");
    }

    [Fact]
    public void Discover_ControllerWithAuthorize_SetsAuthorizationInfo()
    {
        var code = """
            using Microsoft.AspNetCore.Mvc;
            using Microsoft.AspNetCore.Authorization;

            [ApiController]
            [Route("api/[controller]")]
            [Authorize]
            public class SecureController : ControllerBase
            {
                [HttpGet]
                public IActionResult GetSecure() => Ok();
            }
            """;

        var tree = SourceFileLoader.ParseText(code);
        var endpoints = _discoverer.Discover(tree).ToList();

        endpoints.Should().HaveCount(1);
        endpoints[0].Authorization.IsEffectivelyAuthorized.Should().BeTrue();
        endpoints[0].Classification.Should().Be(SecurityClassification.Authenticated);
    }

    [Fact]
    public void Discover_ActionWithAuthorizeRoles_SetsRoles()
    {
        var code = """
            using Microsoft.AspNetCore.Mvc;
            using Microsoft.AspNetCore.Authorization;

            [ApiController]
            [Route("api/[controller]")]
            public class AdminController : ControllerBase
            {
                [HttpGet]
                [Authorize(Roles = "Admin,Manager")]
                public IActionResult AdminOnly() => Ok();
            }
            """;

        var tree = SourceFileLoader.ParseText(code);
        var endpoints = _discoverer.Discover(tree).ToList();

        endpoints.Should().HaveCount(1);
        endpoints[0].Authorization.Roles.Should().BeEquivalentTo(["Admin", "Manager"]);
        endpoints[0].Classification.Should().Be(SecurityClassification.RoleRestricted);
    }

    [Fact]
    public void Discover_ActionWithAuthorizePolicy_SetsPolicy()
    {
        var code = """
            using Microsoft.AspNetCore.Mvc;
            using Microsoft.AspNetCore.Authorization;

            [ApiController]
            [Route("api/[controller]")]
            public class PolicyController : ControllerBase
            {
                [HttpGet]
                [Authorize(Policy = "CanReadData")]
                public IActionResult PolicyProtected() => Ok();
            }
            """;

        var tree = SourceFileLoader.ParseText(code);
        var endpoints = _discoverer.Discover(tree).ToList();

        endpoints.Should().HaveCount(1);
        endpoints[0].Authorization.Policies.Should().Contain("CanReadData");
        endpoints[0].Classification.Should().Be(SecurityClassification.PolicyRestricted);
    }

    [Fact]
    public void Discover_ActionWithAllowAnonymous_OverridesControllerAuth()
    {
        var code = """
            using Microsoft.AspNetCore.Mvc;
            using Microsoft.AspNetCore.Authorization;

            [ApiController]
            [Route("api/[controller]")]
            [Authorize]
            public class MixedController : ControllerBase
            {
                [HttpGet("secure")]
                public IActionResult Secure() => Ok();

                [HttpGet("public")]
                [AllowAnonymous]
                public IActionResult Public() => Ok();
            }
            """;

        var tree = SourceFileLoader.ParseText(code);
        var endpoints = _discoverer.Discover(tree).ToList();

        endpoints.Should().HaveCount(2);

        var secureEndpoint = endpoints.First(e => e.ActionName == "Secure");
        secureEndpoint.Classification.Should().Be(SecurityClassification.Authenticated);

        var publicEndpoint = endpoints.First(e => e.ActionName == "Public");
        publicEndpoint.Authorization.HasAllowAnonymous.Should().BeTrue();
        publicEndpoint.Classification.Should().Be(SecurityClassification.Public);
    }

    [Fact]
    public void Discover_ClassWithoutControllerSuffix_IgnoresIfNoAttribute()
    {
        var code = """
            using Microsoft.AspNetCore.Mvc;

            public class ItemService
            {
                [HttpGet]
                public IActionResult SomeMethod() => null;
            }
            """;

        var tree = SourceFileLoader.ParseText(code);
        var endpoints = _discoverer.Discover(tree).ToList();

        endpoints.Should().BeEmpty();
    }

    [Fact]
    public void Discover_ClassWithApiControllerAttribute_IsDiscovered()
    {
        var code = """
            using Microsoft.AspNetCore.Mvc;

            [ApiController]
            [Route("api/items")]
            public class ItemsApi : ControllerBase
            {
                [HttpGet]
                public IActionResult Get() => Ok();
            }
            """;

        var tree = SourceFileLoader.ParseText(code);
        var endpoints = _discoverer.Discover(tree).ToList();

        endpoints.Should().HaveCount(1);
    }

    [Fact]
    public void Discover_RouteTemplateWithTokens_ResolvesTokens()
    {
        var code = """
            using Microsoft.AspNetCore.Mvc;

            [ApiController]
            [Route("api/[controller]/[action]")]
            public class UsersController : ControllerBase
            {
                [HttpGet]
                public IActionResult List() => Ok();
            }
            """;

        var tree = SourceFileLoader.ParseText(code);
        var endpoints = _discoverer.Discover(tree).ToList();

        endpoints.Should().HaveCount(1);
        endpoints[0].Route.Should().Be("/api/Users/List");
    }
}
