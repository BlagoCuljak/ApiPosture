using ApiPosture.Core.Models;
using ApiPosture.Rules.Surface;
using FluentAssertions;
using HttpMethod = ApiPosture.Core.Models.HttpMethod;

namespace ApiPosture.Rules.Tests.Surface;

public class MinimalApiWithoutAuthRuleTests
{
    private readonly MinimalApiWithoutAuthRule _rule = new();

    [Fact]
    public void RuleId_IsAP008()
    {
        _rule.RuleId.Should().Be("AP008");
    }

    [Fact]
    public void DefaultSeverity_IsHigh()
    {
        _rule.DefaultSeverity.Should().Be(Severity.High);
    }

    [Fact]
    public void Evaluate_MinimalApiWithoutAuth_ReturnsFinding()
    {
        var endpoint = CreateEndpoint(EndpointType.MinimalApi, hasAuthorize: false, hasAllowAnonymous: false);

        var finding = _rule.Evaluate(endpoint);

        finding.Should().NotBeNull();
        finding!.RuleId.Should().Be("AP008");
    }

    [Fact]
    public void Evaluate_MinimalApiWithRequireAuthorization_ReturnsNull()
    {
        var endpoint = CreateEndpoint(EndpointType.MinimalApi, hasAuthorize: true, hasAllowAnonymous: false);

        var finding = _rule.Evaluate(endpoint);

        finding.Should().BeNull();
    }

    [Fact]
    public void Evaluate_MinimalApiWithAllowAnonymous_ReturnsNull()
    {
        var endpoint = CreateEndpoint(EndpointType.MinimalApi, hasAuthorize: false, hasAllowAnonymous: true);

        var finding = _rule.Evaluate(endpoint);

        finding.Should().BeNull();
    }

    [Fact]
    public void Evaluate_ControllerEndpoint_ReturnsNull()
    {
        var endpoint = CreateEndpoint(EndpointType.Controller, hasAuthorize: false, hasAllowAnonymous: false);

        var finding = _rule.Evaluate(endpoint);

        finding.Should().BeNull();
    }

    private static Endpoint CreateEndpoint(EndpointType type, bool hasAuthorize, bool hasAllowAnonymous)
    {
        return new Endpoint
        {
            Route = "/api/test",
            Methods = HttpMethod.Get,
            Type = type,
            Location = new SourceLocation("test.cs", 1),
            Authorization = new AuthorizationInfo
            {
                HasAuthorize = hasAuthorize,
                HasAllowAnonymous = hasAllowAnonymous
            },
            Classification = hasAuthorize ? SecurityClassification.Authenticated : SecurityClassification.Public
        };
    }
}
