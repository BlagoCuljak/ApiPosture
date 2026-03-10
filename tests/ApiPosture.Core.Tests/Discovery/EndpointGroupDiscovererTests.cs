using ApiPosture.Core.Analysis;
using ApiPosture.Core.Discovery;
using ApiPosture.Core.Models;
using FluentAssertions;
using HttpMethod = ApiPosture.Core.Models.HttpMethod;

namespace ApiPosture.Core.Tests.Discovery;

/// <summary>
/// Tests for <see cref="EndpointGroupDiscoverer"/> — the handler-first, class-based
/// Minimal API pattern used by the Clean Architecture template.
/// </summary>
public class EndpointGroupDiscovererTests
{
    private readonly EndpointGroupDiscoverer _discoverer = new();

    // ─── Basic discovery ───────────────────────────────────────────────────

    [Fact]
    public void Discover_HandlerFirst_NoRoute_ReturnsEndpointAtGroupRoot()
    {
        // groupBuilder.MapGet(Handler) — route defaults to class name root
        var code = """
            public class TodoLists : EndpointGroupBase
            {
                public override void Map(RouteGroupBuilder groupBuilder)
                {
                    groupBuilder.MapGet(GetAll).RequireAuthorization();
                }
                public static IResult GetAll() => Results.Ok();
            }
            """;

        var tree = SourceFileLoader.ParseText(code);
        var endpoints = _discoverer.Discover(tree).ToList();

        endpoints.Should().HaveCount(1);
        endpoints[0].Route.Should().Be("/TodoLists");
        endpoints[0].Methods.Should().Be(HttpMethod.Get);
        endpoints[0].Type.Should().Be(EndpointType.MinimalApi);
    }

    [Fact]
    public void Discover_HandlerFirst_WithRoute_CombinesRoutes()
    {
        // groupBuilder.MapPut(Handler, "{id}") — route is second arg
        var code = """
            public class TodoLists : EndpointGroupBase
            {
                public override void Map(RouteGroupBuilder groupBuilder)
                {
                    groupBuilder.MapPut(Update, "{id}").RequireAuthorization();
                }
                public static IResult Update(int id) => Results.NoContent();
            }
            """;

        var tree = SourceFileLoader.ParseText(code);
        var endpoints = _discoverer.Discover(tree).ToList();

        endpoints.Should().HaveCount(1);
        endpoints[0].Route.Should().Be("/TodoLists/{id}");
        endpoints[0].Methods.Should().Be(HttpMethod.Put);
    }

    [Fact]
    public void Discover_StandardSignature_FirstArgString_ReturnsEndpoint()
    {
        // groupBuilder.MapGet("/route", handler) — standard Minimal API ordering still works
        var code = """
            public class Items : EndpointGroupBase
            {
                public override void Map(RouteGroupBuilder groupBuilder)
                {
                    groupBuilder.MapGet("/items", GetItems).RequireAuthorization();
                }
                public static IResult GetItems() => Results.Ok();
            }
            """;

        var tree = SourceFileLoader.ParseText(code);
        var endpoints = _discoverer.Discover(tree).ToList();

        endpoints.Should().HaveCount(1);
        endpoints[0].Route.Should().Be("/Items/items");
    }

    [Fact]
    public void Discover_MultipleEndpoints_ReturnsAll()
    {
        var code = """
            public class TodoLists : EndpointGroupBase
            {
                public override void Map(RouteGroupBuilder groupBuilder)
                {
                    groupBuilder.MapGet(GetAll).RequireAuthorization();
                    groupBuilder.MapPost(Create).RequireAuthorization();
                    groupBuilder.MapPut(Update, "{id}").RequireAuthorization();
                    groupBuilder.MapDelete(Delete, "{id}").RequireAuthorization();
                }
                public static IResult GetAll() => Results.Ok();
                public static IResult Create() => Results.Created();
                public static IResult Update(int id) => Results.NoContent();
                public static IResult Delete(int id) => Results.NoContent();
            }
            """;

        var tree = SourceFileLoader.ParseText(code);
        var endpoints = _discoverer.Discover(tree).ToList();

        endpoints.Should().HaveCount(4);
        endpoints.Should().ContainSingle(e => e.Methods == HttpMethod.Get);
        endpoints.Should().ContainSingle(e => e.Methods == HttpMethod.Post);
        endpoints.Should().ContainSingle(e => e.Methods == HttpMethod.Put);
        endpoints.Should().ContainSingle(e => e.Methods == HttpMethod.Delete);
    }

    // ─── Authorization ────────────────────────────────────────────────────

    [Fact]
    public void Discover_PerEndpointRequireAuthorization_IsAuthenticated()
    {
        var code = """
            public class TodoLists : EndpointGroupBase
            {
                public override void Map(RouteGroupBuilder groupBuilder)
                {
                    groupBuilder.MapGet(GetAll).RequireAuthorization();
                }
                public static IResult GetAll() => Results.Ok();
            }
            """;

        var tree = SourceFileLoader.ParseText(code);
        var endpoint = _discoverer.Discover(tree).Single();

        endpoint.Classification.Should().Be(SecurityClassification.Authenticated);
    }

    [Fact]
    public void Discover_GroupLevelRequireAuthorization_InheritedByAllEndpoints()
    {
        // WeatherForecasts pattern: groupBuilder.RequireAuthorization(); applied once
        var code = """
            public class WeatherForecasts : EndpointGroupBase
            {
                public override void Map(RouteGroupBuilder groupBuilder)
                {
                    groupBuilder.RequireAuthorization();
                    groupBuilder.MapGet(GetForecasts);
                }
                public static IResult GetForecasts() => Results.Ok();
            }
            """;

        var tree = SourceFileLoader.ParseText(code);
        var endpoint = _discoverer.Discover(tree).Single();

        endpoint.Classification.Should().Be(SecurityClassification.Authenticated);
        endpoint.Authorization.IsEffectivelyAuthorized.Should().BeTrue();
    }

    [Fact]
    public void Discover_NoAuthorization_IsPublic()
    {
        var code = """
            public class PublicEndpoints : EndpointGroupBase
            {
                public override void Map(RouteGroupBuilder groupBuilder)
                {
                    groupBuilder.MapGet(GetPublic);
                }
                public static IResult GetPublic() => Results.Ok();
            }
            """;

        var tree = SourceFileLoader.ParseText(code);
        var endpoint = _discoverer.Discover(tree).Single();

        endpoint.Classification.Should().Be(SecurityClassification.Public);
    }

    // ─── Abstract class filtering ──────────────────────────────────────────

    [Fact]
    public void Discover_AbstractClass_IsSkipped()
    {
        var code = """
            public abstract class EndpointGroupBase
            {
                public abstract void Map(RouteGroupBuilder groupBuilder);
            }
            """;

        var tree = SourceFileLoader.ParseText(code);
        var endpoints = _discoverer.Discover(tree).ToList();

        endpoints.Should().BeEmpty();
    }

    // ─── IEndpointRouteBuilder parameter variant ──────────────────────────

    [Fact]
    public void Discover_IEndpointRouteBuilder_Parameter_Discovered()
    {
        var code = """
            public class Users : EndpointGroupBase
            {
                public override void Map(IEndpointRouteBuilder groupBuilder)
                {
                    groupBuilder.MapPost(Login).AllowAnonymous();
                }
                public static IResult Login() => Results.Ok();
            }
            """;

        var tree = SourceFileLoader.ParseText(code);
        var endpoints = _discoverer.Discover(tree).ToList();

        endpoints.Should().HaveCount(1);
        endpoints[0].Route.Should().Be("/Users");
        endpoints[0].Methods.Should().Be(HttpMethod.Post);
    }
}
