using ApiPosture.Core.Models;
using ApiPosture.Rules.Exposure;
using FluentAssertions;
using HttpMethod = ApiPosture.Core.Models.HttpMethod;

namespace ApiPosture.Rules.Tests.Exposure;

public class PublicWithoutExplicitIntentRuleTests
{
    private readonly PublicWithoutExplicitIntentRule _rule = new();

    [Fact]
    public void RuleId_IsAP001()
    {
        _rule.RuleId.Should().Be("AP001");
    }

    [Fact]
    public void DefaultSeverity_IsHigh()
    {
        _rule.DefaultSeverity.Should().Be(Severity.High);
    }

    [Fact]
    public void Evaluate_PublicWithoutAllowAnonymous_ReturnsFinding()
    {
        var endpoint = CreateEndpoint(
            hasAllowAnonymous: false,
            classification: SecurityClassification.Public);

        var finding = _rule.Evaluate(endpoint);

        finding.Should().NotBeNull();
        finding!.RuleId.Should().Be("AP001");
    }

    [Fact]
    public void Evaluate_PublicWithExplicitAllowAnonymous_ReturnsNull()
    {
        var endpoint = CreateEndpoint(
            hasAllowAnonymous: true,
            classification: SecurityClassification.Public);

        var finding = _rule.Evaluate(endpoint);

        finding.Should().BeNull();
    }

    [Fact]
    public void Evaluate_AuthenticatedEndpoint_ReturnsNull()
    {
        var endpoint = CreateEndpoint(
            hasAuthorize: true,
            classification: SecurityClassification.Authenticated);

        var finding = _rule.Evaluate(endpoint);

        finding.Should().BeNull();
    }

    private static Endpoint CreateEndpoint(
        bool hasAuthorize = false,
        bool hasAllowAnonymous = false,
        SecurityClassification classification = SecurityClassification.Public)
    {
        return new Endpoint
        {
            Route = "/api/test",
            Methods = HttpMethod.Get,
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
