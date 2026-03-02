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

// ---------------------------------------------------------------------------
// Map*Endpoints naming support
// ---------------------------------------------------------------------------

public class MapEndpointsNamingTests
{
    private readonly ExtensionMethodDiscoverer _discoverer = new();

    [Fact]
    public void DiscoverExtensionMethods_FindsMapEndpointsSuffixedMethod()
    {
        var code = """
            internal static class RoleEndpoints
            {
                internal static void MapRoleEndpoints(this IEndpointRouteBuilder app)
                {
                    var group = app.MapGroup("/roles").RequireAuthorization();
                    group.MapGet("", () => Results.Ok());
                    group.MapPost("", () => Results.Created());
                }
            }
            """;

        var tree = SourceFileLoader.ParseText(code);
        var methods = _discoverer.DiscoverExtensionMethods(tree).ToList();

        methods.Should().HaveCount(1);
        methods[0].MethodName.Should().Be("MapRoleEndpoints");
        methods[0].ParameterName.Should().Be("app");
    }

    [Fact]
    public void DiscoverExtensionMethods_IgnoresNonMapEndpointsPattern()
    {
        // "ConfigureEndpoints" does not start with "Map" — should still be ignored
        var code = """
            internal static class Utils
            {
                internal static void ConfigureEndpoints(this IEndpointRouteBuilder app)
                {
                    app.MapGet("/test", () => Results.Ok());
                }
            }
            """;

        var tree = SourceFileLoader.ParseText(code);
        var methods = _discoverer.DiscoverExtensionMethods(tree).ToList();

        methods.Should().BeEmpty();
    }

    [Fact]
    public void DiscoverEndpointsInMethod_MapEndpointsSuffix_CombinesGroupRoute()
    {
        var code = """
            internal static class RoleEndpoints
            {
                internal static void MapRoleEndpoints(this IEndpointRouteBuilder app)
                {
                    var group = app.MapGroup("/roles");
                    group.MapGet("", () => Results.Ok());
                    group.MapGet("/{id:guid}", (Guid id) => Results.Ok());
                    group.MapPost("", () => Results.Created());
                    group.MapPut("/{id:guid}", (Guid id) => Results.Ok());
                    group.MapDelete("/{id:guid}", (Guid id) => Results.NoContent());
                }
            }
            """;

        var tree = SourceFileLoader.ParseText(code);
        var methods = _discoverer.DiscoverExtensionMethods(tree).ToList();
        methods.Should().HaveCount(1);

        var endpoints = _discoverer.DiscoverEndpointsInMethod(
            methods[0],
            string.Empty,
            AuthorizationInfo.Empty,
            GlobalAuthorizationInfo.Empty).ToList();

        endpoints.Should().HaveCount(5);
        endpoints.Should().Contain(e => e.Route == "/roles" && e.Methods == HttpMethod.Get);
        endpoints.Should().Contain(e => e.Route == "/roles/{id:guid}" && e.Methods == HttpMethod.Get);
        endpoints.Should().Contain(e => e.Route == "/roles" && e.Methods == HttpMethod.Post);
        endpoints.Should().Contain(e => e.Route == "/roles/{id:guid}" && e.Methods == HttpMethod.Put);
        endpoints.Should().Contain(e => e.Route == "/roles/{id:guid}" && e.Methods == HttpMethod.Delete);
    }

    [Fact]
    public void DiscoverEndpointsInMethod_MapEndpointsSuffix_InheritsGroupAuth()
    {
        var code = """
            internal static class RoleEndpoints
            {
                internal static void MapRoleEndpoints(this IEndpointRouteBuilder app)
                {
                    var group = app.MapGroup("/roles").RequireAuthorization("WebApp");
                    group.MapGet("", () => Results.Ok());
                    group.MapPost("", () => Results.Created());
                }
            }
            """;

        var tree = SourceFileLoader.ParseText(code);
        var methods = _discoverer.DiscoverExtensionMethods(tree).ToList();
        methods.Should().HaveCount(1);

        var endpoints = _discoverer.DiscoverEndpointsInMethod(
            methods[0],
            string.Empty,
            AuthorizationInfo.Empty,
            GlobalAuthorizationInfo.Empty).ToList();

        endpoints.Should().HaveCount(2);
        endpoints.Should().OnlyContain(e => e.Authorization.IsEffectivelyAuthorized);
    }
}

public class RouteGroupRegistryDirectCallTests
{
    private readonly RouteGroupRegistry _registry = new();

    [Fact]
    public void Analyze_RegistersSyntheticGroupForDirectMapEndpointsCall()
    {
        var code = """
            app.MapRoleEndpoints();
            """;

        var tree = SourceFileLoader.ParseText(code);
        _registry.Analyze(tree);

        var groups = _registry.GetGroupsCallingMethod("MapRoleEndpoints").ToList();
        groups.Should().HaveCount(1);
        groups[0].RoutePrefix.Should().BeEmpty();
        groups[0].Authorization.HasAuthorize.Should().BeFalse();
    }

    [Fact]
    public void Analyze_RegistersSyntheticGroupForDirectMapRoutesCall()
    {
        var code = """
            app.MapTasksRoutes();
            """;

        var tree = SourceFileLoader.ParseText(code);
        _registry.Analyze(tree);

        var groups = _registry.GetGroupsCallingMethod("MapTasksRoutes").ToList();
        groups.Should().HaveCount(1);
        groups[0].RoutePrefix.Should().BeEmpty();
    }

    [Fact]
    public void Analyze_RegistersOneSyntheticGroupPerMethodNameEvenIfCalledMultipleTimes()
    {
        // Same method called twice in the same file — only one RouteGroupInfo should be created
        var code = """
            app.MapRoleEndpoints();
            // called again somewhere
            app.MapRoleEndpoints();
            """;

        var tree = SourceFileLoader.ParseText(code);
        _registry.Analyze(tree);

        var groups = _registry.GetGroupsCallingMethod("MapRoleEndpoints").ToList();
        groups.Should().HaveCount(1);
    }

    [Fact]
    public void Analyze_DoesNotRegisterSyntheticGroupForMapGroupVariableCalls()
    {
        // Calls on known MapGroup variables must NOT be duplicated as synthetic root groups
        var code = """
            var adminGroup = app.MapGroup("/admin").RequireAuthorization("Admin");
            adminGroup.MapAdminRoutes();
            """;

        var tree = SourceFileLoader.ParseText(code);
        _registry.Analyze(tree);

        // Should have exactly one group — the real MapGroup one, NOT a synthetic duplicate
        _registry.RouteGroups.Should().HaveCount(1);
        _registry.RouteGroups[0].RoutePrefix.Should().Be("/admin");
        _registry.RouteGroups[0].Authorization.HasAuthorize.Should().BeTrue();
    }

    [Fact]
    public void Analyze_HandlesMultipleDirectEndpointMethodCalls()
    {
        var code = """
            app.MapRoleEndpoints();
            app.MapUserEndpoints();
            app.MapAuthEndpoints();
            """;

        var tree = SourceFileLoader.ParseText(code);
        _registry.Analyze(tree);

        _registry.GetGroupsCallingMethod("MapRoleEndpoints").Should().HaveCount(1);
        _registry.GetGroupsCallingMethod("MapUserEndpoints").Should().HaveCount(1);
        _registry.GetGroupsCallingMethod("MapAuthEndpoints").Should().HaveCount(1);
    }
}

/// <summary>
/// End-to-end integration tests that exercise the full ProjectAnalyzer pipeline
/// with the Map*Endpoints pattern (mirroring the user's real-world code structure).
/// </summary>
public class MapEndpointsIntegrationTests
{
    [Fact]
    public void ProjectAnalyzer_DiscoversEndpoints_WhenRegisteredViaDirectMapEndpointsCall()
    {
        // program.cs: direct call on app
        var programCode = """
            app.MapRoleEndpoints();
            """;

        // RoleEndpoints.cs: extension method with Map*Endpoints suffix
        var endpointsCode = """
            internal static class RoleEndpoints
            {
                internal static void MapRoleEndpoints(this IEndpointRouteBuilder app)
                {
                    var group = app.MapGroup("/api/roles")
                        .RequireAuthorization();

                    group.MapGet("", GetRoles);
                    group.MapGet("/{id:guid}", GetRoleById);
                    group.MapPost("", CreateRole);
                    group.MapPut("/{id:guid}", UpdateRole);
                    group.MapDelete("/{id:guid}", DeleteRole);
                }
            }
            """;

        var trees = new[]
        {
            SourceFileLoader.ParseText(programCode, "Program.cs"),
            SourceFileLoader.ParseText(endpointsCode, "RoleEndpoints.cs")
        };

        var registry = new RouteGroupRegistry();
        var extDiscoverer = new ExtensionMethodDiscoverer();

        foreach (var tree in trees)
            registry.Analyze(tree);

        var extensionMethods = trees.SelectMany(extDiscoverer.DiscoverExtensionMethods).ToList();
        extensionMethods.Should().HaveCount(1, "MapRoleEndpoints should be discovered");

        var globalAuth = GlobalAuthorizationInfo.Empty;
        var allEndpoints = new List<Endpoint>();

        foreach (var method in extensionMethods)
        {
            var groups = registry.GetGroupsCallingMethod(method.MethodName).ToList();
            groups.Should().HaveCount(1, $"{method.MethodName} should have one registered call site");

            foreach (var group in groups)
            {
                allEndpoints.AddRange(
                    extDiscoverer.DiscoverEndpointsInMethod(method, group.RoutePrefix, group.Authorization, globalAuth));
            }
        }

        allEndpoints.Should().HaveCount(5);
        allEndpoints.Should().Contain(e => e.Route == "/api/roles" && e.Methods == HttpMethod.Get);
        allEndpoints.Should().Contain(e => e.Route == "/api/roles/{id:guid}" && e.Methods == HttpMethod.Get);
        allEndpoints.Should().Contain(e => e.Route == "/api/roles" && e.Methods == HttpMethod.Post);
        allEndpoints.Should().Contain(e => e.Route == "/api/roles/{id:guid}" && e.Methods == HttpMethod.Put);
        allEndpoints.Should().Contain(e => e.Route == "/api/roles/{id:guid}" && e.Methods == HttpMethod.Delete);
        allEndpoints.Should().OnlyContain(e => e.Authorization.IsEffectivelyAuthorized,
            "group has RequireAuthorization()");
    }

    [Fact]
    public void ProjectAnalyzer_DiscoversEndpoints_WhenNestedViaRegisterEndpointsCoordinator()
    {
        // Mirrors the exact structure from the issue:
        //   app.RegisterEndpoints() → app.MapAuthEndpoints() → app.MapRoleEndpoints()
        // RegisterEndpoints is not Map* so it's ignored — but MapRoleEndpoints IS discovered
        // because the direct call on app is picked up by AnalyzeDirectExtensionMethodCalls.

        var registerCode = """
            app.RegisterEndpoints();
            """;

        var authGroupCode = """
            internal static class AuthEndpoints
            {
                internal static void MapAuthEndpoints(this IEndpointRouteBuilder app)
                {
                    app.MapRoleEndpoints();
                }
            }
            """;

        var roleCode = """
            internal static class RoleEndpoints
            {
                internal static void MapRoleEndpoints(this IEndpointRouteBuilder app)
                {
                    var group = app.MapGroup("/auth/roles")
                        .RequireAuthorization("WebApp");

                    group.MapGet("", GetRole);
                    group.MapPost("", CreateRole);
                }
            }
            """;

        var trees = new[]
        {
            SourceFileLoader.ParseText(registerCode, "Program.cs"),
            SourceFileLoader.ParseText(authGroupCode, "AuthEndpoints.cs"),
            SourceFileLoader.ParseText(roleCode, "RoleEndpoints.cs")
        };

        var registry = new RouteGroupRegistry();
        var extDiscoverer = new ExtensionMethodDiscoverer();

        foreach (var tree in trees)
            registry.Analyze(tree);

        var extensionMethods = trees.SelectMany(extDiscoverer.DiscoverExtensionMethods).ToList();

        // MapRoleEndpoints is discovered (Map*Endpoints). MapAuthEndpoints also qualifies.
        extensionMethods.Select(m => m.MethodName).Should().Contain("MapRoleEndpoints");

        var globalAuth = GlobalAuthorizationInfo.Empty;
        var allEndpoints = new List<Endpoint>();

        foreach (var method in extensionMethods)
        {
            foreach (var group in registry.GetGroupsCallingMethod(method.MethodName))
            {
                allEndpoints.AddRange(
                    extDiscoverer.DiscoverEndpointsInMethod(method, group.RoutePrefix, group.Authorization, globalAuth));
            }
        }

        allEndpoints.Should().Contain(e => e.Route == "/auth/roles" && e.Methods == HttpMethod.Get);
        allEndpoints.Should().Contain(e => e.Route == "/auth/roles" && e.Methods == HttpMethod.Post);
        allEndpoints.Should().OnlyContain(e => e.Authorization.IsEffectivelyAuthorized);
    }
}
