using ApiPosture.Core.Models;
using ApiPosture.Rules.Exposure;
using FluentAssertions;
using HttpMethod = ApiPosture.Core.Models.HttpMethod;

namespace ApiPosture.Rules.Tests.Exposure;

public class AllowAnonymousOnWriteRuleTests
{
    private readonly AllowAnonymousOnWriteRule _rule = new();

    [Fact]
    public void RuleId_IsAP002()
    {
        _rule.RuleId.Should().Be("AP002");
    }

    [Fact]
    public void Evaluate_AllowAnonymousOnPost_ReturnsFinding()
    {
        var endpoint = CreateEndpoint(HttpMethod.Post, hasAllowAnonymous: true);

        var finding = _rule.Evaluate(endpoint);

        finding.Should().NotBeNull();
        finding!.RuleId.Should().Be("AP002");
    }

    [Fact]
    public void Evaluate_AllowAnonymousOnPut_ReturnsFinding()
    {
        var endpoint = CreateEndpoint(HttpMethod.Put, hasAllowAnonymous: true);

        var finding = _rule.Evaluate(endpoint);

        finding.Should().NotBeNull();
    }

    [Fact]
    public void Evaluate_AllowAnonymousOnDelete_ReturnsFinding()
    {
        var endpoint = CreateEndpoint(HttpMethod.Delete, hasAllowAnonymous: true);

        var finding = _rule.Evaluate(endpoint);

        finding.Should().NotBeNull();
    }

    [Fact]
    public void Evaluate_AllowAnonymousOnGet_ReturnsNull()
    {
        var endpoint = CreateEndpoint(HttpMethod.Get, hasAllowAnonymous: true);

        var finding = _rule.Evaluate(endpoint);

        finding.Should().BeNull();
    }

    [Fact]
    public void Evaluate_PostWithoutAllowAnonymous_ReturnsNull()
    {
        var endpoint = CreateEndpoint(HttpMethod.Post, hasAllowAnonymous: false);

        var finding = _rule.Evaluate(endpoint);

        finding.Should().BeNull();
    }

    private static Endpoint CreateEndpoint(HttpMethod method, bool hasAllowAnonymous)
    {
        return new Endpoint
        {
            Route = "/api/test",
            Methods = method,
            Type = EndpointType.Controller,
            Location = new SourceLocation("test.cs", 1),
            Authorization = new AuthorizationInfo
            {
                HasAllowAnonymous = hasAllowAnonymous
            },
            Classification = hasAllowAnonymous ? SecurityClassification.Public : SecurityClassification.Authenticated
        };
    }
}
