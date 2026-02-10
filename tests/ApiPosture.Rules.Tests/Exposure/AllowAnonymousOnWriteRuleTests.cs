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
        var endpoint = CreateEndpoint("/api/test", HttpMethod.Post, hasAllowAnonymous: true);

        var finding = _rule.Evaluate(endpoint);

        finding.Should().NotBeNull();
        finding!.RuleId.Should().Be("AP002");
    }

    [Fact]
    public void Evaluate_AllowAnonymousOnPut_ReturnsFinding()
    {
        var endpoint = CreateEndpoint("/api/test", HttpMethod.Put, hasAllowAnonymous: true);

        var finding = _rule.Evaluate(endpoint);

        finding.Should().NotBeNull();
    }

    [Fact]
    public void Evaluate_AllowAnonymousOnDelete_ReturnsFinding()
    {
        var endpoint = CreateEndpoint("/api/test", HttpMethod.Delete, hasAllowAnonymous: true);

        var finding = _rule.Evaluate(endpoint);

        finding.Should().NotBeNull();
    }

    [Fact]
    public void Evaluate_AllowAnonymousOnGet_ReturnsNull()
    {
        var endpoint = CreateEndpoint("/api/test", HttpMethod.Get, hasAllowAnonymous: true);

        var finding = _rule.Evaluate(endpoint);

        finding.Should().BeNull();
    }

    [Fact]
    public void Evaluate_PostWithoutAllowAnonymous_ReturnsNull()
    {
        var endpoint = CreateEndpoint("/api/test", HttpMethod.Post, hasAllowAnonymous: false);

        var finding = _rule.Evaluate(endpoint);

        finding.Should().BeNull();
    }

    [Fact]
    public void Evaluate_WebhookEndpoint_ReturnsMediumSeverity()
    {
        var endpoint = CreateEndpoint("/api/payments/webhook/stripe", HttpMethod.Post, hasAllowAnonymous: true);

        var finding = _rule.Evaluate(endpoint);

        finding.Should().NotBeNull();
        finding!.Severity.Should().Be(Severity.Medium);
        finding.Message.Should().Contain("signature validation");
    }

    [Fact]
    public void Evaluate_ViewCounterEndpoint_ReturnsLowSeverity()
    {
        var endpoint = CreateEndpoint("/api/prayers/{id}/increment-view", HttpMethod.Post, hasAllowAnonymous: true);

        var finding = _rule.Evaluate(endpoint);

        finding.Should().NotBeNull();
        finding!.Severity.Should().Be(Severity.Low);
        finding.Message.Should().Contain("rate limiting");
    }

    [Fact]
    public void Evaluate_TokenRegistrationEndpoint_ReturnsMediumSeverity()
    {
        var endpoint = CreateEndpoint("/api/push/register-token", HttpMethod.Post, hasAllowAnonymous: true);

        var finding = _rule.Evaluate(endpoint);

        finding.Should().NotBeNull();
        finding!.Severity.Should().Be(Severity.Medium);
    }

    [Fact]
    public void Evaluate_RegularWriteEndpoint_ReturnsHighSeverity()
    {
        var endpoint = CreateEndpoint("/api/users/{id}", HttpMethod.Post, hasAllowAnonymous: true);

        var finding = _rule.Evaluate(endpoint);

        finding.Should().NotBeNull();
        finding!.Severity.Should().Be(Severity.High);
    }

    private static Endpoint CreateEndpoint(string route, HttpMethod method, bool hasAllowAnonymous)
    {
        return new Endpoint
        {
            Route = route,
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
