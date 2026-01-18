using ApiPosture.Core.Models;
using ApiPosture.Rules.Consistency;
using FluentAssertions;
using HttpMethod = ApiPosture.Core.Models.HttpMethod;

namespace ApiPosture.Rules.Tests.Consistency;

public class MissingAuthOnWritesRuleTests
{
    private readonly MissingAuthOnWritesRule _rule = new();

    [Fact]
    public void RuleId_IsAP004()
    {
        _rule.RuleId.Should().Be("AP004");
    }

    [Fact]
    public void DefaultSeverity_IsCritical()
    {
        _rule.DefaultSeverity.Should().Be(Severity.Critical);
    }

    [Fact]
    public void Evaluate_PublicPostWithoutAuth_ReturnsFinding()
    {
        var endpoint = CreateEndpoint(
            methods: HttpMethod.Post,
            hasAllowAnonymous: false,
            classification: SecurityClassification.Public);

        var finding = _rule.Evaluate(endpoint);

        finding.Should().NotBeNull();
        finding!.Severity.Should().Be(Severity.Critical);
    }

    [Fact]
    public void Evaluate_PublicDeleteWithoutAuth_ReturnsFinding()
    {
        var endpoint = CreateEndpoint(
            methods: HttpMethod.Delete,
            hasAllowAnonymous: false,
            classification: SecurityClassification.Public);

        var finding = _rule.Evaluate(endpoint);

        finding.Should().NotBeNull();
    }

    [Fact]
    public void Evaluate_PublicGetWithoutAuth_ReturnsNull()
    {
        var endpoint = CreateEndpoint(
            methods: HttpMethod.Get,
            hasAllowAnonymous: false,
            classification: SecurityClassification.Public);

        var finding = _rule.Evaluate(endpoint);

        finding.Should().BeNull();
    }

    [Fact]
    public void Evaluate_PostWithExplicitAllowAnonymous_ReturnsNull()
    {
        // AP002 handles this case instead
        var endpoint = CreateEndpoint(
            methods: HttpMethod.Post,
            hasAllowAnonymous: true,
            classification: SecurityClassification.Public);

        var finding = _rule.Evaluate(endpoint);

        finding.Should().BeNull();
    }

    [Fact]
    public void Evaluate_AuthenticatedPost_ReturnsNull()
    {
        var endpoint = CreateEndpoint(
            methods: HttpMethod.Post,
            hasAuthorize: true,
            classification: SecurityClassification.Authenticated);

        var finding = _rule.Evaluate(endpoint);

        finding.Should().BeNull();
    }

    private static Endpoint CreateEndpoint(
        HttpMethod methods,
        bool hasAuthorize = false,
        bool hasAllowAnonymous = false,
        SecurityClassification classification = SecurityClassification.Public)
    {
        return new Endpoint
        {
            Route = "/api/test",
            Methods = methods,
            Type = EndpointType.Controller,
            Location = new SourceLocation("test.cs", 1),
            Authorization = new AuthorizationInfo
            {
                HasAuthorize = hasAuthorize,
                HasAllowAnonymous = hasAllowAnonymous
            },
            Classification = classification
        };
    }
}
