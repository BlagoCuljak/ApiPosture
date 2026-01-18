using ApiPosture.Core.Models;
using HttpMethod = ApiPosture.Core.Models.HttpMethod;

namespace ApiPosture.Core.Grouping;

/// <summary>
/// Provides grouping functionality for endpoints and findings.
/// </summary>
public sealed class EndpointGrouper
{
    private readonly GroupOptions _options;

    public EndpointGrouper(GroupOptions options)
    {
        _options = options;
    }

    /// <summary>
    /// Groups endpoints according to the configured options.
    /// </summary>
    public IReadOnlyList<EndpointGroup>? GroupEndpoints(IEnumerable<Endpoint> endpoints)
    {
        if (!_options.HasEndpointGrouping)
            return null;

        var groups = _options.EndpointGroupBy!.Value switch
        {
            GroupField.Controller => GroupByController(endpoints),
            GroupField.Classification => GroupByClassification(endpoints),
            GroupField.Method => GroupByMethod(endpoints),
            GroupField.Type => GroupByType(endpoints),
            GroupField.Severity => GroupByClassification(endpoints), // Fallback to classification for endpoints
            _ => null
        };

        return groups?.ToList();
    }

    /// <summary>
    /// Groups findings according to the configured options.
    /// </summary>
    public IReadOnlyList<FindingGroup>? GroupFindings(IEnumerable<Finding> findings)
    {
        if (!_options.HasFindingGrouping)
            return null;

        var groups = _options.FindingGroupBy!.Value switch
        {
            GroupField.Severity => GroupFindingsBySeverity(findings),
            GroupField.Controller => GroupFindingsByController(findings),
            GroupField.Classification => GroupFindingsByClassification(findings),
            GroupField.Method => GroupFindingsByMethod(findings),
            GroupField.Type => GroupFindingsByType(findings),
            _ => null
        };

        return groups?.ToList();
    }

    private IEnumerable<EndpointGroup> GroupByController(IEnumerable<Endpoint> endpoints)
    {
        var grouped = endpoints.GroupBy(e => e.ControllerName ?? "Minimal API Endpoints");

        foreach (var group in grouped.OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            var displayName = group.Key == "Minimal API Endpoints"
                ? "Minimal API Endpoints"
                : group.Key;

            yield return new EndpointGroup
            {
                Key = group.Key,
                DisplayName = displayName,
                Endpoints = group.ToList()
            };
        }
    }

    private IEnumerable<EndpointGroup> GroupByClassification(IEnumerable<Endpoint> endpoints)
    {
        var order = new[] {
            SecurityClassification.Public,
            SecurityClassification.Authenticated,
            SecurityClassification.RoleRestricted,
            SecurityClassification.PolicyRestricted
        };

        var grouped = endpoints.GroupBy(e => e.Classification);

        foreach (var classification in order)
        {
            var group = grouped.FirstOrDefault(g => g.Key == classification);
            if (group is not null)
            {
                yield return new EndpointGroup
                {
                    Key = classification.ToString(),
                    DisplayName = GetClassificationDisplayName(classification),
                    Endpoints = group.ToList()
                };
            }
        }
    }

    private IEnumerable<EndpointGroup> GroupByMethod(IEnumerable<Endpoint> endpoints)
    {
        var methods = new[] {
            HttpMethod.Get,
            HttpMethod.Post,
            HttpMethod.Put,
            HttpMethod.Delete,
            HttpMethod.Patch,
            HttpMethod.Head,
            HttpMethod.Options
        };

        foreach (var method in methods)
        {
            var matching = endpoints.Where(e => e.Methods.HasFlag(method)).ToList();
            if (matching.Count > 0)
            {
                yield return new EndpointGroup
                {
                    Key = method.ToString(),
                    DisplayName = method.ToString().ToUpperInvariant(),
                    Endpoints = matching
                };
            }
        }
    }

    private IEnumerable<EndpointGroup> GroupByType(IEnumerable<Endpoint> endpoints)
    {
        var grouped = endpoints.GroupBy(e => e.Type);

        foreach (var group in grouped.OrderBy(g => g.Key))
        {
            yield return new EndpointGroup
            {
                Key = group.Key.ToString(),
                DisplayName = group.Key == EndpointType.Controller ? "Controller Endpoints" : "Minimal API Endpoints",
                Endpoints = group.ToList()
            };
        }
    }

    private IEnumerable<FindingGroup> GroupFindingsBySeverity(IEnumerable<Finding> findings)
    {
        var order = new[] {
            Severity.Critical,
            Severity.High,
            Severity.Medium,
            Severity.Low,
            Severity.Info
        };

        var grouped = findings.GroupBy(f => f.Severity);

        foreach (var severity in order)
        {
            var group = grouped.FirstOrDefault(g => g.Key == severity);
            if (group is not null)
            {
                yield return new FindingGroup
                {
                    Key = severity.ToString(),
                    DisplayName = GetSeverityDisplayName(severity),
                    Findings = group.ToList()
                };
            }
        }
    }

    private IEnumerable<FindingGroup> GroupFindingsByController(IEnumerable<Finding> findings)
    {
        var grouped = findings.GroupBy(f => f.Endpoint.ControllerName ?? "Minimal API Endpoints");

        foreach (var group in grouped.OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            yield return new FindingGroup
            {
                Key = group.Key,
                DisplayName = group.Key,
                Findings = group.ToList()
            };
        }
    }

    private IEnumerable<FindingGroup> GroupFindingsByClassification(IEnumerable<Finding> findings)
    {
        var order = new[] {
            SecurityClassification.Public,
            SecurityClassification.Authenticated,
            SecurityClassification.RoleRestricted,
            SecurityClassification.PolicyRestricted
        };

        var grouped = findings.GroupBy(f => f.Endpoint.Classification);

        foreach (var classification in order)
        {
            var group = grouped.FirstOrDefault(g => g.Key == classification);
            if (group is not null)
            {
                yield return new FindingGroup
                {
                    Key = classification.ToString(),
                    DisplayName = GetClassificationDisplayName(classification),
                    Findings = group.ToList()
                };
            }
        }
    }

    private IEnumerable<FindingGroup> GroupFindingsByMethod(IEnumerable<Finding> findings)
    {
        var methods = new[] {
            HttpMethod.Get,
            HttpMethod.Post,
            HttpMethod.Put,
            HttpMethod.Delete,
            HttpMethod.Patch
        };

        foreach (var method in methods)
        {
            var matching = findings.Where(f => f.Endpoint.Methods.HasFlag(method)).ToList();
            if (matching.Count > 0)
            {
                yield return new FindingGroup
                {
                    Key = method.ToString(),
                    DisplayName = method.ToString().ToUpperInvariant(),
                    Findings = matching
                };
            }
        }
    }

    private IEnumerable<FindingGroup> GroupFindingsByType(IEnumerable<Finding> findings)
    {
        var grouped = findings.GroupBy(f => f.Endpoint.Type);

        foreach (var group in grouped.OrderBy(g => g.Key))
        {
            yield return new FindingGroup
            {
                Key = group.Key.ToString(),
                DisplayName = group.Key == EndpointType.Controller ? "Controller Endpoints" : "Minimal API Endpoints",
                Findings = group.ToList()
            };
        }
    }

    private static string GetClassificationDisplayName(SecurityClassification classification)
    {
        return classification switch
        {
            SecurityClassification.Public => "Public Endpoints",
            SecurityClassification.Authenticated => "Authenticated Endpoints",
            SecurityClassification.RoleRestricted => "Role-Restricted Endpoints",
            SecurityClassification.PolicyRestricted => "Policy-Restricted Endpoints",
            _ => classification.ToString()
        };
    }

    private static string GetSeverityDisplayName(Severity severity)
    {
        return severity switch
        {
            Severity.Critical => "Critical Issues",
            Severity.High => "High Severity Issues",
            Severity.Medium => "Medium Severity Issues",
            Severity.Low => "Low Severity Issues",
            Severity.Info => "Informational",
            _ => severity.ToString()
        };
    }

    /// <summary>
    /// Creates a grouper with default options (no grouping).
    /// </summary>
    public static EndpointGrouper Default { get; } = new(GroupOptions.Default);
}
