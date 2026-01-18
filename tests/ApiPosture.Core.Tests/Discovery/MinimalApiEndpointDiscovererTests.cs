using ApiPosture.Core.Analysis;
using ApiPosture.Core.Discovery;
using ApiPosture.Core.Models;
using FluentAssertions;
using HttpMethod = ApiPosture.Core.Models.HttpMethod;

namespace ApiPosture.Core.Tests.Discovery;

public class MinimalApiEndpointDiscovererTests
{
    private readonly MinimalApiEndpointDiscoverer _discoverer = new();

    [Fact]
    public void Discover_MapGet_ReturnsEndpoint()
    {
        var code = """
            var app = WebApplication.Create();
            app.MapGet("/api/items", () => Results.Ok());
            """;

        var tree = SourceFileLoader.ParseText(code);
        var endpoints = _discoverer.Discover(tree).ToList();

        endpoints.Should().HaveCount(1);
        endpoints[0].Route.Should().Be("/api/items");
        endpoints[0].Methods.Should().Be(HttpMethod.Get);
        endpoints[0].Type.Should().Be(EndpointType.MinimalApi);
    }

    [Fact]
    public void Discover_MapPost_ReturnsPostEndpoint()
    {
        var code = """
            var app = WebApplication.Create();
            app.MapPost("/api/items", () => Results.Created());
            """;

        var tree = SourceFileLoader.ParseText(code);
        var endpoints = _discoverer.Discover(tree).ToList();

        endpoints.Should().HaveCount(1);
        endpoints[0].Methods.Should().Be(HttpMethod.Post);
    }

    [Fact]
    public void Discover_MapPut_ReturnsPutEndpoint()
    {
        var code = """
            var app = WebApplication.Create();
            app.MapPut("/api/items/{id}", (int id) => Results.Ok());
            """;

        var tree = SourceFileLoader.ParseText(code);
        var endpoints = _discoverer.Discover(tree).ToList();

        endpoints.Should().HaveCount(1);
        endpoints[0].Methods.Should().Be(HttpMethod.Put);
        endpoints[0].Route.Should().Be("/api/items/{id}");
    }

    [Fact]
    public void Discover_MapDelete_ReturnsDeleteEndpoint()
    {
        var code = """
            var app = WebApplication.Create();
            app.MapDelete("/api/items/{id}", (int id) => Results.NoContent());
            """;

        var tree = SourceFileLoader.ParseText(code);
        var endpoints = _discoverer.Discover(tree).ToList();

        endpoints.Should().HaveCount(1);
        endpoints[0].Methods.Should().Be(HttpMethod.Delete);
    }

    [Fact]
    public void Discover_WithRequireAuthorization_SetsAuthInfo()
    {
        var code = """
            var app = WebApplication.Create();
            app.MapGet("/api/secure", () => Results.Ok())
               .RequireAuthorization();
            """;

        var tree = SourceFileLoader.ParseText(code);
        var endpoints = _discoverer.Discover(tree).ToList();

        endpoints.Should().HaveCount(1);
        endpoints[0].Authorization.HasAuthorize.Should().BeTrue();
        endpoints[0].Classification.Should().Be(SecurityClassification.Authenticated);
    }

    [Fact]
    public void Discover_WithRequireAuthorizationPolicy_SetsPolicy()
    {
        var code = """
            var app = WebApplication.Create();
            app.MapGet("/api/admin", () => Results.Ok())
               .RequireAuthorization("AdminPolicy");
            """;

        var tree = SourceFileLoader.ParseText(code);
        var endpoints = _discoverer.Discover(tree).ToList();

        endpoints.Should().HaveCount(1);
        endpoints[0].Authorization.Policies.Should().Contain("AdminPolicy");
        endpoints[0].Classification.Should().Be(SecurityClassification.PolicyRestricted);
    }

    [Fact]
    public void Discover_WithAllowAnonymous_SetsAllowAnonymous()
    {
        var code = """
            var app = WebApplication.Create();
            app.MapGet("/api/public", () => Results.Ok())
               .AllowAnonymous();
            """;

        var tree = SourceFileLoader.ParseText(code);
        var endpoints = _discoverer.Discover(tree).ToList();

        endpoints.Should().HaveCount(1);
        endpoints[0].Authorization.HasAllowAnonymous.Should().BeTrue();
        endpoints[0].Classification.Should().Be(SecurityClassification.Public);
    }

    [Fact]
    public void Discover_MultipleEndpoints_ReturnsAll()
    {
        var code = """
            var app = WebApplication.Create();
            app.MapGet("/api/items", () => Results.Ok());
            app.MapPost("/api/items", () => Results.Created());
            app.MapDelete("/api/items/{id}", (int id) => Results.NoContent());
            """;

        var tree = SourceFileLoader.ParseText(code);
        var endpoints = _discoverer.Discover(tree).ToList();

        endpoints.Should().HaveCount(3);
        endpoints.Should().Contain(e => e.Methods == HttpMethod.Get);
        endpoints.Should().Contain(e => e.Methods == HttpMethod.Post);
        endpoints.Should().Contain(e => e.Methods == HttpMethod.Delete);
    }

    [Fact]
    public void Discover_NoMapCalls_ReturnsEmpty()
    {
        var code = """
            var app = WebApplication.Create();
            app.Run();
            """;

        var tree = SourceFileLoader.ParseText(code);
        var endpoints = _discoverer.Discover(tree).ToList();

        endpoints.Should().BeEmpty();
    }

    [Fact]
    public void Discover_WithoutAuth_ClassifiesAsPublic()
    {
        var code = """
            var app = WebApplication.Create();
            app.MapGet("/api/open", () => Results.Ok());
            """;

        var tree = SourceFileLoader.ParseText(code);
        var endpoints = _discoverer.Discover(tree).ToList();

        endpoints.Should().HaveCount(1);
        endpoints[0].Classification.Should().Be(SecurityClassification.Public);
        endpoints[0].Authorization.IsEffectivelyAuthorized.Should().BeFalse();
    }
}
