using ApiPosture.Core.Models;
using ApiPosture.Rules.Surface;
using FluentAssertions;
using HttpMethod = ApiPosture.Core.Models.HttpMethod;

namespace ApiPosture.Rules.Tests.Surface;

public class SensitiveRouteKeywordsRuleTests
{
    private readonly SensitiveRouteKeywordsRule _rule = new();

    [Fact]
    public void RuleId_IsAP007()
    {
        _rule.RuleId.Should().Be("AP007");
    }

    [Theory]
    [InlineData("/api/admin")]
    [InlineData("/api/admin/users")]
    [InlineData("/admin/dashboard")]
    [InlineData("/api/debug")]
    [InlineData("/api/export")]
    [InlineData("/api/config")]
    [InlineData("/api/settings")]
    [InlineData("/internal/api")]
    public void Evaluate_PublicRouteWithSensitiveKeyword_ReturnsFinding(string route)
    {
        var endpoint = CreateEndpoint(route, SecurityClassification.Public);

        var finding = _rule.Evaluate(endpoint);

        finding.Should().NotBeNull();
        finding!.RuleId.Should().Be("AP007");
    }

    [Theory]
    [InlineData("/api/products")]
    [InlineData("/api/users")]
    [InlineData("/api/orders")]
    [InlineData("/health")]
    [InlineData("/api/v1/items")]
    public void Evaluate_PublicRouteWithoutSensitiveKeyword_ReturnsNull(string route)
    {
        var endpoint = CreateEndpoint(route, SecurityClassification.Public);

        var finding = _rule.Evaluate(endpoint);

        finding.Should().BeNull();
    }

    [Fact]
    public void Evaluate_AuthenticatedAdminRoute_ReturnsNull()
    {
        var endpoint = CreateEndpoint("/api/admin", SecurityClassification.Authenticated);

        var finding = _rule.Evaluate(endpoint);

        finding.Should().BeNull();
    }

    private static Endpoint CreateEndpoint(string route, SecurityClassification classification)
    {
        return new Endpoint
        {
            Route = route,
            Methods = HttpMethod.Get,
            Type = EndpointType.Controller,
            Location = new SourceLocation("test.cs", 1),
            Authorization = AuthorizationInfo.Empty,
            Classification = classification
        };
    }
}
