# ApiPosture

A cross-platform CLI security inspection tool for ASP.NET Core APIs. Performs static source-code analysis using Roslyn to identify authorization misconfigurations and security risks.

## Features

- Static analysis of ASP.NET Core projects (no compilation required)
- Discovers endpoints from both Controllers and Minimal APIs
- Detects 8 common security issues with authorization
- Multiple output formats: Terminal, JSON, Markdown
- Sorting, filtering, and grouping of results
- Configuration file support with suppressions
- Accessibility options (no-color, no-icons)
- CI/CD integration with `--fail-on` exit codes
- Cross-platform .NET Global Tool

## Installation

```bash
# Install as global tool
dotnet tool install --global ApiPosture

# Or from local build
dotnet pack src/ApiPosture -c Release
dotnet tool install --global --add-source ./src/ApiPosture/nupkg ApiPosture
```

## Usage

```bash
# Scan current directory
apiposture scan .

# Scan specific project
apiposture scan ./src/MyWebApi

# Output as JSON
apiposture scan . --output json

# Output as Markdown report
apiposture scan . --output markdown --output-file report.md

# Filter by severity
apiposture scan . --severity medium

# CI integration - fail if high severity findings
apiposture scan . --fail-on high

# Sorting
apiposture scan . --sort-by route --sort-dir asc

# Filtering
apiposture scan . --classification public --method POST,DELETE
apiposture scan . --route-contains admin --api-style controller

# Grouping
apiposture scan . --group-by controller
apiposture scan . --group-findings-by severity

# Accessibility (no colors/icons)
apiposture scan . --no-color --no-icons

# Use config file
apiposture scan . --config .apiposture.json
```

## Configuration File

Create `.apiposture.json` in your project root:

```json
{
  "severity": { "default": "low", "failOn": "high" },
  "suppressions": [
    { "route": "/api/health", "rules": ["AP001"], "reason": "Intentionally public" }
  ],
  "rules": {
    "AP007": { "sensitiveKeywords": ["admin", "debug", "secret"] }
  },
  "display": { "useColors": true, "useIcons": true }
}
```

## Security Rules

| Rule ID | Name | Severity | Description |
|---------|------|----------|-------------|
| AP001 | Public without explicit intent | High | Endpoint is publicly accessible without `[AllowAnonymous]` |
| AP002 | AllowAnonymous on write | High | `[AllowAnonymous]` on POST/PUT/DELETE operations |
| AP003 | Controller/action conflict | Medium | `[AllowAnonymous]` overrides controller-level `[Authorize]` |
| AP004 | Missing auth on writes | Critical | Public POST/PUT/PATCH/DELETE without authorization |
| AP005 | Excessive role access | Low | More than 3 roles on single endpoint |
| AP006 | Weak role naming | Low | Generic role names like "User", "Admin" |
| AP007 | Sensitive route keywords | Medium | `admin`, `debug`, `export` in public routes |
| AP008 | Minimal API without auth | High | Minimal API endpoint with no auth chain |

## Example Output

```
╭─────────────────┬────────────────────────────────╮
│ Metric          │ Value                          │
├─────────────────┼────────────────────────────────┤
│ Scanned Path    │ /path/to/project               │
│ Files Scanned   │ 15                             │
│ Endpoints Found │ 42                             │
│ Total Findings  │ 8                              │
│ Scan Duration   │ 250ms                          │
╰─────────────────┴────────────────────────────────╯

╭──────────────────────┬─────────┬────────────┬──────────────────╮
│ Route                │ Methods │ Type       │ Classification   │
├──────────────────────┼─────────┼────────────┼──────────────────┤
│ /api/products        │ GET     │ Controller │ Authenticated    │
│ /api/admin           │ GET     │ Controller │ Public           │
│ /api/orders          │ POST    │ MinimalApi │ PolicyRestricted │
╰──────────────────────┴─────────┴────────────┴──────────────────╯
```

## Project Structure

```
ApiPosture/
├── src/
│   ├── ApiPosture/           # CLI application (Global Tool)
│   ├── ApiPosture.Core/      # Analysis engine
│   └── ApiPosture.Rules/     # Security rules
├── tests/
│   ├── ApiPosture.Core.Tests/
│   └── ApiPosture.Rules.Tests/
└── samples/SampleWebApi/     # Example project for testing
```

## Building from Source

```bash
# Restore and build
dotnet restore
dotnet build

# Run tests
dotnet test

# Run against sample project
dotnet run --project src/ApiPosture -- scan samples/SampleWebApi
```

## License

MIT
