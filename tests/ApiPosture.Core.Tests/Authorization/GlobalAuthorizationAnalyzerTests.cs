using ApiPosture.Core.Analysis;
using ApiPosture.Core.Authorization;
using FluentAssertions;

namespace ApiPosture.Core.Tests.Authorization;

public class GlobalAuthorizationAnalyzerTests
{
    private readonly GlobalAuthorizationAnalyzer _analyzer = new();

    [Fact]
    public void Analyze_WithFallbackPolicyRequireAuthenticatedUser_DetectsFallbackPolicy()
    {
        var code = """
            builder.Services.AddAuthorization(options =>
            {
                options.FallbackPolicy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build();
            });
            """;

        var tree = SourceFileLoader.ParseText(code);
        var result = _analyzer.Analyze([tree]);

        result.HasFallbackPolicy.Should().BeTrue();
        result.FallbackRequiresAuthentication.Should().BeTrue();
        result.ProtectsAllEndpointsByDefault.Should().BeTrue();
    }

    [Fact]
    public void Analyze_WithFallbackPolicyRequireRole_DetectsRoles()
    {
        var code = """
            builder.Services.AddAuthorization(options =>
            {
                options.FallbackPolicy = new AuthorizationPolicyBuilder()
                    .RequireRole("Admin")
                    .Build();
            });
            """;

        var tree = SourceFileLoader.ParseText(code);
        var result = _analyzer.Analyze([tree]);

        result.HasFallbackPolicy.Should().BeTrue();
        result.FallbackRoles.Should().Contain("Admin");
        result.ProtectsAllEndpointsByDefault.Should().BeTrue();
    }

    [Fact]
    public void Analyze_WithFallbackPolicyRequireMultipleRoles_DetectsAllRoles()
    {
        var code = """
            builder.Services.AddAuthorization(options =>
            {
                options.FallbackPolicy = new AuthorizationPolicyBuilder()
                    .RequireRole("Admin", "Manager")
                    .Build();
            });
            """;

        var tree = SourceFileLoader.ParseText(code);
        var result = _analyzer.Analyze([tree]);

        result.HasFallbackPolicy.Should().BeTrue();
        result.FallbackRoles.Should().Contain("Admin");
        result.FallbackRoles.Should().Contain("Manager");
    }

    [Fact]
    public void Analyze_WithFallbackPolicyRequireClaim_DetectsClaims()
    {
        var code = """
            builder.Services.AddAuthorization(options =>
            {
                options.FallbackPolicy = new AuthorizationPolicyBuilder()
                    .RequireClaim("email")
                    .Build();
            });
            """;

        var tree = SourceFileLoader.ParseText(code);
        var result = _analyzer.Analyze([tree]);

        result.HasFallbackPolicy.Should().BeTrue();
        result.FallbackClaims.Should().Contain("email");
        result.ProtectsAllEndpointsByDefault.Should().BeTrue();
    }

    [Fact]
    public void Analyze_WithoutFallbackPolicy_ReturnsNoFallbackPolicy()
    {
        var code = """
            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
            });
            """;

        var tree = SourceFileLoader.ParseText(code);
        var result = _analyzer.Analyze([tree]);

        result.HasFallbackPolicy.Should().BeFalse();
        result.ProtectsAllEndpointsByDefault.Should().BeFalse();
    }

    [Fact]
    public void Analyze_WithDefaultPolicy_DetectsDefaultPolicy()
    {
        var code = """
            builder.Services.AddAuthorization(options =>
            {
                options.DefaultPolicy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build();
            });
            """;

        var tree = SourceFileLoader.ParseText(code);
        var result = _analyzer.Analyze([tree]);

        result.HasDefaultPolicy.Should().BeTrue();
        // DefaultPolicy doesn't protect all endpoints by itself
        result.HasFallbackPolicy.Should().BeFalse();
    }

    [Fact]
    public void Analyze_WithNoAddAuthorization_ReturnsEmpty()
    {
        var code = """
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            """;

        var tree = SourceFileLoader.ParseText(code);
        var result = _analyzer.Analyze([tree]);

        result.HasFallbackPolicy.Should().BeFalse();
        result.HasDefaultPolicy.Should().BeFalse();
        result.ProtectsAllEndpointsByDefault.Should().BeFalse();
    }

    [Fact]
    public void Analyze_WithAddAuthorizationNoLambda_ReturnsEmpty()
    {
        var code = """
            builder.Services.AddAuthorization();
            """;

        var tree = SourceFileLoader.ParseText(code);
        var result = _analyzer.Analyze([tree]);

        result.HasFallbackPolicy.Should().BeFalse();
        result.ProtectsAllEndpointsByDefault.Should().BeFalse();
    }

    [Fact]
    public void Analyze_WithParenthesizedLambda_DetectsFallbackPolicy()
    {
        // Analyzer supports parenthesized lambda expressions (used when parameter types are explicit)
        var code = """
            builder.Services.AddAuthorization((AuthorizationOptions options) =>
            {
                options.FallbackPolicy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build();
            });
            """;

        var tree = SourceFileLoader.ParseText(code);
        var result = _analyzer.Analyze([tree]);

        result.HasFallbackPolicy.Should().BeTrue();
        result.FallbackRequiresAuthentication.Should().BeTrue();
    }

    [Fact]
    public void Analyze_WithSetFallbackPolicy_DetectsFallbackPolicy()
    {
        var code = """
            builder.Services.AddAuthorizationBuilder()
                .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build());
            """;

        var tree = SourceFileLoader.ParseText(code);
        var result = _analyzer.Analyze([tree]);

        result.HasFallbackPolicy.Should().BeTrue();
        result.FallbackRequiresAuthentication.Should().BeTrue();
        result.ProtectsAllEndpointsByDefault.Should().BeTrue();
    }

    [Fact]
    public void Analyze_WithSetDefaultPolicy_DetectsDefaultPolicy()
    {
        var code = """
            builder.Services.AddAuthorizationBuilder()
                .SetDefaultPolicy(new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build());
            """;

        var tree = SourceFileLoader.ParseText(code);
        var result = _analyzer.Analyze([tree]);

        result.HasDefaultPolicy.Should().BeTrue();
        result.HasFallbackPolicy.Should().BeFalse();
    }

    [Fact]
    public void Analyze_WithCombinedPolicyRequirements_DetectsAll()
    {
        var code = """
            builder.Services.AddAuthorization(options =>
            {
                options.FallbackPolicy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .RequireRole("User")
                    .RequireClaim("email")
                    .Build();
            });
            """;

        var tree = SourceFileLoader.ParseText(code);
        var result = _analyzer.Analyze([tree]);

        result.HasFallbackPolicy.Should().BeTrue();
        result.FallbackRequiresAuthentication.Should().BeTrue();
        result.FallbackRoles.Should().Contain("User");
        result.FallbackClaims.Should().Contain("email");
        result.ProtectsAllEndpointsByDefault.Should().BeTrue();
    }

    [Fact]
    public void ToAuthorizationInfo_WithFallbackPolicy_ReturnsCorrectInfo()
    {
        var globalAuth = new GlobalAuthorizationInfo
        {
            HasFallbackPolicy = true,
            FallbackRequiresAuthentication = true,
            FallbackRoles = ["Admin", "Manager"]
        };

        var authInfo = globalAuth.ToAuthorizationInfo();

        authInfo.HasAuthorize.Should().BeTrue();
        authInfo.HasAllowAnonymous.Should().BeFalse();
        authInfo.Roles.Should().Contain("Admin");
        authInfo.Roles.Should().Contain("Manager");
    }

    [Fact]
    public void ToAuthorizationInfo_WithoutFallbackPolicy_ReturnsEmpty()
    {
        var globalAuth = new GlobalAuthorizationInfo
        {
            HasFallbackPolicy = false
        };

        var authInfo = globalAuth.ToAuthorizationInfo();

        authInfo.HasAuthorize.Should().BeFalse();
        authInfo.HasAllowAnonymous.Should().BeFalse();
        authInfo.Roles.Should().BeEmpty();
        authInfo.Policies.Should().BeEmpty();
        authInfo.IsEffectivelyAuthorized.Should().BeFalse();
    }
}
