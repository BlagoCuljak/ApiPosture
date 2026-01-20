using ApiPosture.Core.Analysis;
using ApiPosture.Core.Authorization;
using ApiPosture.Core.Discovery;
using ApiPosture.Core.Models;
using FluentAssertions;
using HttpMethod = ApiPosture.Core.Models.HttpMethod;

namespace ApiPosture.Core.Tests.Discovery;

public class ExtensionMethodDiscovererTests
{
    private readonly ExtensionMethodDiscoverer _discoverer = new();

    [Fact]
    public void DiscoverExtensionMethods_FindsMapRoutesExtension()
    {
        var code = """
            public static class TasksRoutes
            {
                public static void MapTasksRoutes(this IEndpointRouteBuilder root)
                {
                    root.MapGet("/tasks", () => Results.Ok());
                }
            }
            """;

        var tree = SourceFileLoader.ParseText(code);
        var methods = _discoverer.DiscoverExtensionMethods(tree).ToList();

        methods.Should().HaveCount(1);
        methods[0].MethodName.Should().Be("MapTasksRoutes");
        methods[0].ParameterName.Should().Be("root");
    }

    [Fact]
    public void DiscoverExtensionMethods_FindsRouteGroupBuilderExtension()
    {
        var code = """
            public static class UsersRoutes
            {
                public static void MapUsersRoutes(this RouteGroupBuilder group)
                {
                    group.MapGet("/users", () => Results.Ok());
                }
            }
            """;

        var tree = SourceFileLoader.ParseText(code);
        var methods = _discoverer.DiscoverExtensionMethods(tree).ToList();

        methods.Should().HaveCount(1);
        methods[0].MethodName.Should().Be("MapUsersRoutes");
        methods[0].ExtendedType.Should().Contain("RouteGroupBuilder");
    }

    [Fact]
    public void DiscoverExtensionMethods_IgnoresNonExtensionMethods()
    {
        var code = """
            public static class Routes
            {
                public static void MapTasksRoutes(IEndpointRouteBuilder root)
                {
                    root.MapGet("/tasks", () => Results.Ok());
                }
            }
            """;

        var tree = SourceFileLoader.ParseText(code);
        var methods = _discoverer.DiscoverExtensionMethods(tree).ToList();

        methods.Should().BeEmpty();
    }

    [Fact]
    public void DiscoverExtensionMethods_IgnoresNonMapRoutesPattern()
    {
        var code = """
            public static class Utils
            {
                public static void ConfigureRoutes(this IEndpointRouteBuilder root)
                {
                    root.MapGet("/test", () => Results.Ok());
                }
            }
            """;

        var tree = SourceFileLoader.ParseText(code);
        var methods = _discoverer.DiscoverExtensionMethods(tree).ToList();

        methods.Should().BeEmpty();
    }

    [Fact]
    public void DiscoverEndpointsInMethod_DiscoverEndpointsWithParentPrefix()
    {
        var code = """
            public static class TasksRoutes
            {
                public static void MapTasksRoutes(this IEndpointRouteBuilder root)
                {
                    root.MapGet("/tasks", () => Results.Ok());
                    root.MapPost("/tasks", () => Results.Created());
                }
            }
            """;

        var tree = SourceFileLoader.ParseText(code);
        var methods = _discoverer.DiscoverExtensionMethods(tree).ToList();

        var parentAuth = AuthorizationInfo.Empty;
        var globalAuth = GlobalAuthorizationInfo.Empty;

        var endpoints = _discoverer.DiscoverEndpointsInMethod(
            methods[0],
            "/api/v1",
            parentAuth,
            globalAuth).ToList();

        endpoints.Should().HaveCount(2);
        endpoints.Should().Contain(e => e.Route == "/api/v1/tasks" && e.Methods == HttpMethod.Get);
        endpoints.Should().Contain(e => e.Route == "/api/v1/tasks" && e.Methods == HttpMethod.Post);
    }

    [Fact]
    public void DiscoverEndpointsInMethod_InheritsParentAuthorization()
    {
        var code = """
            public static class TasksRoutes
            {
                public static void MapTasksRoutes(this IEndpointRouteBuilder root)
                {
                    root.MapGet("/tasks", () => Results.Ok());
                }
            }
            """;

        var tree = SourceFileLoader.ParseText(code);
        var methods = _discoverer.DiscoverExtensionMethods(tree).ToList();

        var parentAuth = new AuthorizationInfo
        {
            HasAuthorize = true,
            Policies = ["AdminPolicy"]
        };

        var endpoints = _discoverer.DiscoverEndpointsInMethod(
            methods[0],
            "/api",
            parentAuth,
            GlobalAuthorizationInfo.Empty).ToList();

        endpoints.Should().HaveCount(1);
        endpoints[0].Authorization.InheritedFrom.Should().NotBeNull();
        endpoints[0].Authorization.IsEffectivelyAuthorized.Should().BeTrue();
    }

    [Fact]
    public void DiscoverEndpointsInMethod_InheritsGlobalFallbackPolicy()
    {
        var code = """
            public static class TasksRoutes
            {
                public static void MapTasksRoutes(this IEndpointRouteBuilder root)
                {
                    root.MapGet("/tasks", () => Results.Ok());
                }
            }
            """;

        var tree = SourceFileLoader.ParseText(code);
        var methods = _discoverer.DiscoverExtensionMethods(tree).ToList();

        var globalAuth = new GlobalAuthorizationInfo
        {
            HasFallbackPolicy = true,
            FallbackRequiresAuthentication = true
        };

        var endpoints = _discoverer.DiscoverEndpointsInMethod(
            methods[0],
            "/api",
            AuthorizationInfo.Empty,
            globalAuth).ToList();

        endpoints.Should().HaveCount(1);
        endpoints[0].Authorization.IsEffectivelyAuthorized.Should().BeTrue();
        endpoints[0].Classification.Should().Be(SecurityClassification.Authenticated);
    }

    [Fact]
    public void DiscoverEndpointsInMethod_RespectsExplicitAllowAnonymous()
    {
        var code = """
            public static class TasksRoutes
            {
                public static void MapTasksRoutes(this IEndpointRouteBuilder root)
                {
                    root.MapGet("/public", () => Results.Ok()).AllowAnonymous();
                }
            }
            """;

        var tree = SourceFileLoader.ParseText(code);
        var methods = _discoverer.DiscoverExtensionMethods(tree).ToList();

        var globalAuth = new GlobalAuthorizationInfo
        {
            HasFallbackPolicy = true,
            FallbackRequiresAuthentication = true
        };

        var endpoints = _discoverer.DiscoverEndpointsInMethod(
            methods[0],
            "/api",
            AuthorizationInfo.Empty,
            globalAuth).ToList();

        endpoints.Should().HaveCount(1);
        endpoints[0].Authorization.HasAllowAnonymous.Should().BeTrue();
        endpoints[0].Classification.Should().Be(SecurityClassification.Public);
    }

    [Fact]
    public void DiscoverEndpointsInMethod_DirectCallsOnParameter()
    {
        // Extension method discoverer works best with direct calls on the parameter
        // (root.MapGet, root.MapPost, etc.)
        var code = """
            public static class TasksRoutes
            {
                public static void MapTasksRoutes(this IEndpointRouteBuilder root)
                {
                    root.MapGet("/tasks", () => Results.Ok());
                    root.MapGet("/tasks/{id}", (int id) => Results.Ok());
                }
            }
            """;

        var tree = SourceFileLoader.ParseText(code);
        var methods = _discoverer.DiscoverExtensionMethods(tree).ToList();

        var endpoints = _discoverer.DiscoverEndpointsInMethod(
            methods[0],
            "/api",
            AuthorizationInfo.Empty,
            GlobalAuthorizationInfo.Empty).ToList();

        endpoints.Should().HaveCount(2);
        endpoints.Should().Contain(e => e.Route == "/api/tasks");
        endpoints.Should().Contain(e => e.Route == "/api/tasks/{id}");
    }

    [Fact]
    public void DiscoverEndpointsInMethod_FluentChainFromParameter()
    {
        // Supports fluent chain directly from parameter like root.MapGroup(...).MapGet(...)
        var code = """
            public static class TasksRoutes
            {
                public static void MapTasksRoutes(this IEndpointRouteBuilder root)
                {
                    root.MapGroup("/tasks").MapGet("", () => Results.Ok());
                }
            }
            """;

        var tree = SourceFileLoader.ParseText(code);
        var methods = _discoverer.DiscoverExtensionMethods(tree).ToList();

        var endpoints = _discoverer.DiscoverEndpointsInMethod(
            methods[0],
            "/api",
            AuthorizationInfo.Empty,
            GlobalAuthorizationInfo.Empty).ToList();

        endpoints.Should().HaveCount(1);
        endpoints[0].Route.Should().Be("/api/tasks");
    }

    [Fact]
    public void DiscoverEndpointsInMethod_VariableAssignedMapGroup_CombinesRoutePrefix()
    {
        var code = """
            public static class TasksRoutes
            {
                public static void MapTasksRoutes(this IEndpointRouteBuilder root)
                {
                    var group = root.MapGroup("/tasks");
                    group.MapGet("", () => Results.Ok());
                    group.MapGet("/{id}", (int id) => Results.Ok());
                }
            }
            """;

        var tree = SourceFileLoader.ParseText(code);
        var methods = _discoverer.DiscoverExtensionMethods(tree).ToList();

        var endpoints = _discoverer.DiscoverEndpointsInMethod(
            methods[0],
            "/api/v1",
            AuthorizationInfo.Empty,
            GlobalAuthorizationInfo.Empty).ToList();

        endpoints.Should().HaveCount(2);
        endpoints.Should().Contain(e => e.Route == "/api/v1/tasks");
        endpoints.Should().Contain(e => e.Route == "/api/v1/tasks/{id}");
    }

    [Fact]
    public void DiscoverEndpointsInMethod_NestedVariableMapGroups_CombinesAllPrefixes()
    {
        var code = """
            public static class UsersRoutes
            {
                public static void MapUsersRoutes(this IEndpointRouteBuilder root)
                {
                    var group = root.MapGroup("/users");
                    var adminGroup = group.MapGroup("/admin");
                    adminGroup.MapPost("", () => Results.Created());
                    adminGroup.MapDelete("/{id}", (int id) => Results.NoContent());
                }
            }
            """;

        var tree = SourceFileLoader.ParseText(code);
        var methods = _discoverer.DiscoverExtensionMethods(tree).ToList();

        var endpoints = _discoverer.DiscoverEndpointsInMethod(
            methods[0],
            "/api",
            AuthorizationInfo.Empty,
            GlobalAuthorizationInfo.Empty).ToList();

        endpoints.Should().HaveCount(2);
        endpoints.Should().Contain(e => e.Route == "/api/users/admin");
        endpoints.Should().Contain(e => e.Route == "/api/users/admin/{id}");
    }

    [Fact]
    public void DiscoverEndpointsInMethod_MixedDirectAndVariableGroups_CombinesCorrectly()
    {
        var code = """
            public static class MixedRoutes
            {
                public static void MapMixedRoutes(this IEndpointRouteBuilder root)
                {
                    root.MapGet("/health", () => Results.Ok());
                    var group = root.MapGroup("/items");
                    group.MapGet("", () => Results.Ok());
                    root.MapGroup("/inline").MapGet("/test", () => Results.Ok());
                }
            }
            """;

        var tree = SourceFileLoader.ParseText(code);
        var methods = _discoverer.DiscoverExtensionMethods(tree).ToList();

        var endpoints = _discoverer.DiscoverEndpointsInMethod(
            methods[0],
            "/api",
            AuthorizationInfo.Empty,
            GlobalAuthorizationInfo.Empty).ToList();

        endpoints.Should().HaveCount(3);
        endpoints.Should().Contain(e => e.Route == "/api/health");
        endpoints.Should().Contain(e => e.Route == "/api/items");
        endpoints.Should().Contain(e => e.Route == "/api/inline/test");
    }
}

public class RouteGroupRegistryTests
{
    private readonly RouteGroupRegistry _registry = new();

    [Fact]
    public void Analyze_FindsMapGroupWithExtensionCall()
    {
        var code = """
            var apiGroup = app.MapGroup("/api/v1");
            apiGroup.MapTasksRoutes();
            apiGroup.MapUsersRoutes();
            """;

        var tree = SourceFileLoader.ParseText(code);
        _registry.Analyze(tree);

        _registry.RouteGroups.Should().HaveCount(1);
        var group = _registry.RouteGroups[0];
        group.VariableName.Should().Be("apiGroup");
        group.RoutePrefix.Should().Be("/api/v1");
        group.ExtensionMethodCalls.Should().Contain("MapTasksRoutes");
        group.ExtensionMethodCalls.Should().Contain("MapUsersRoutes");
    }

    [Fact]
    public void Analyze_FindsMapGroupWithAuthorization()
    {
        var code = """
            var adminGroup = app.MapGroup("/admin").RequireAuthorization("AdminPolicy");
            adminGroup.MapAdminRoutes();
            """;

        var tree = SourceFileLoader.ParseText(code);
        _registry.Analyze(tree);

        _registry.RouteGroups.Should().HaveCount(1);
        var group = _registry.RouteGroups[0];
        group.Authorization.HasAuthorize.Should().BeTrue();
        group.Authorization.Policies.Should().Contain("AdminPolicy");
    }

    [Fact]
    public void Analyze_FindsMultipleMapGroups()
    {
        var code = """
            var publicGroup = app.MapGroup("/public");
            publicGroup.MapPublicRoutes();

            var privateGroup = app.MapGroup("/private").RequireAuthorization();
            privateGroup.MapPrivateRoutes();
            """;

        var tree = SourceFileLoader.ParseText(code);
        _registry.Analyze(tree);

        _registry.RouteGroups.Should().HaveCount(2);
        _registry.RouteGroups.Should().Contain(g => g.VariableName == "publicGroup");
        _registry.RouteGroups.Should().Contain(g => g.VariableName == "privateGroup");
    }

    [Fact]
    public void GetGroupsCallingMethod_ReturnsMatchingGroups()
    {
        var code = """
            var apiGroup = app.MapGroup("/api");
            apiGroup.MapTasksRoutes();

            var adminGroup = app.MapGroup("/admin");
            adminGroup.MapAdminRoutes();
            """;

        var tree = SourceFileLoader.ParseText(code);
        _registry.Analyze(tree);

        var tasksGroups = _registry.GetGroupsCallingMethod("MapTasksRoutes").ToList();
        var adminGroups = _registry.GetGroupsCallingMethod("MapAdminRoutes").ToList();

        tasksGroups.Should().HaveCount(1);
        tasksGroups[0].RoutePrefix.Should().Be("/api");

        adminGroups.Should().HaveCount(1);
        adminGroups[0].RoutePrefix.Should().Be("/admin");
    }

    [Fact]
    public void GetGroupsCallingMethod_ReturnsEmptyForUnknownMethod()
    {
        var code = """
            var apiGroup = app.MapGroup("/api");
            apiGroup.MapTasksRoutes();
            """;

        var tree = SourceFileLoader.ParseText(code);
        _registry.Analyze(tree);

        var groups = _registry.GetGroupsCallingMethod("MapUnknownRoutes").ToList();

        groups.Should().BeEmpty();
    }
}
