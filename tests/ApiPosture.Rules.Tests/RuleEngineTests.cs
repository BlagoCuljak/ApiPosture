using ApiPosture.Core.Models;
using ApiPosture.Rules;
using FluentAssertions;
using HttpMethod = ApiPosture.Core.Models.HttpMethod;

namespace ApiPosture.Rules.Tests;

public class RuleEngineTests
{
    private readonly RuleEngine _engine = new();

    [Fact]
    public void Rules_ContainsAllExpectedRules()
    {
        _engine.Rules.Should().HaveCount(8);
        _engine.Rules.Select(r => r.RuleId).Should().BeEquivalentTo([
            "AP001", "AP002", "AP003", "AP004", "AP005", "AP006", "AP007", "AP008"
        ]);
    }

    [Fact]
    public void Evaluate_EmptyEndpoints_ReturnsNoFindings()
    {
        var findings = _engine.Evaluate([]);

        findings.Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_SecureEndpoint_ReturnsNoFindings()
    {
        var endpoint = CreateEndpoint(
            route: "/api/secure",
            methods: HttpMethod.Get,
            type: EndpointType.Controller,
            hasAuthorize: true,
            classification: SecurityClassification.Authenticated);

        var findings = _engine.Evaluate([endpoint]);

        findings.Should().BeEmpty();
    }

    private static Endpoint CreateEndpoint(
        string route,
        HttpMethod methods,
        EndpointType type,
        bool hasAuthorize = false,
        bool hasAllowAnonymous = false,
        IReadOnlyList<string>? roles = null,
        IReadOnlyList<string>? policies = null,
        SecurityClassification classification = SecurityClassification.Public,
        AuthorizationInfo? inheritedFrom = null)
    {
        return new Endpoint
        {
            Route = route,
            Methods = methods,
            Type = type,
            Location = new SourceLocation("test.cs", 1),
            Authorization = new AuthorizationInfo
            {
                HasAuthorize = hasAuthorize,
                HasAllowAnonymous = hasAllowAnonymous,
                Roles = roles ?? [],
                Policies = policies ?? [],
                InheritedFrom = inheritedFrom
            },
            Classification = classification
        };
    }
}
