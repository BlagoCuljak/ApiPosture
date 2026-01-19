# Contributing to ApiPosture

Thank you for your interest in contributing to ApiPosture! This document provides guidelines and information for contributors.

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Getting Started](#getting-started)
- [Development Setup](#development-setup)
- [How to Contribute](#how-to-contribute)
- [Pull Request Process](#pull-request-process)
- [Coding Standards](#coding-standards)
- [Adding New Security Rules](#adding-new-security-rules)
- [Testing](#testing)
- [Questions?](#questions)

## Code of Conduct

This project adheres to a [Code of Conduct](CODE_OF_CONDUCT.md). By participating, you are expected to uphold this code. Please report unacceptable behavior to the maintainers.

## Getting Started

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- Git
- A code editor (VS Code, Visual Studio, Rider, etc.)

### Development Setup

1. **Fork the repository** on GitHub

2. **Clone your fork**:
   ```bash
   git clone https://github.com/YOUR-USERNAME/ApiPosture.git
   cd ApiPosture
   ```

3. **Add upstream remote**:
   ```bash
   git remote add upstream https://github.com/BlagoCuljak/ApiPosture.git
   ```

4. **Build the project**:
   ```bash
   dotnet restore
   dotnet build
   ```

5. **Run tests**:
   ```bash
   dotnet test
   ```

6. **Run against sample project**:
   ```bash
   dotnet run --project src/ApiPosture -- scan samples/SampleWebApi
   ```

## How to Contribute

### Reporting Bugs

1. Check if the bug has already been reported in [Issues](https://github.com/BlagoCuljak/ApiPosture/issues)
2. If not, create a new issue using the **Bug Report** template
3. Provide as much detail as possible, including:
   - Steps to reproduce
   - Expected vs actual behavior
   - Environment details
   - Sample code if applicable

### Suggesting Features

1. Check existing [Issues](https://github.com/BlagoCuljak/ApiPosture/issues) for similar suggestions
2. Create a new issue using the **Feature Request** template
3. Describe the problem you're solving and your proposed solution

### Submitting Code

1. Create a feature branch from `main`:
   ```bash
   git checkout main
   git pull upstream main
   git checkout -b feature/your-feature-name
   ```

2. Make your changes following our [Coding Standards](#coding-standards)

3. Write or update tests as needed

4. Ensure all tests pass:
   ```bash
   dotnet test
   ```

5. Commit your changes with a descriptive message:
   ```bash
   git commit -m "Add feature: description of your changes"
   ```

6. Push to your fork:
   ```bash
   git push origin feature/your-feature-name
   ```

7. Open a Pull Request against `main`

## Pull Request Process

1. **Fill out the PR template** completely
2. **Link related issues** using "Fixes #123" syntax
3. **Ensure CI passes** - all tests must pass
4. **Request review** from maintainers
5. **Address feedback** - make requested changes
6. **Squash commits** if requested before merge

### PR Requirements

- [ ] All tests pass
- [ ] Code follows project style
- [ ] New features include tests
- [ ] Documentation updated if needed
- [ ] No breaking changes without discussion

## Coding Standards

### General Guidelines

- Use meaningful variable and method names
- Keep methods focused and small
- Add XML documentation for public APIs
- Prefer `readonly` and immutability where possible

### C# Style

We follow standard C# conventions:

```csharp
// Use file-scoped namespaces
namespace ApiPosture.Core.Models;

// XML documentation for public types
/// <summary>
/// Represents a discovered API endpoint.
/// </summary>
public class Endpoint
{
    // Properties with expression bodies when simple
    public string Route { get; init; } = string.Empty;
    
    // Prefer init-only setters for immutability
    public required string Method { get; init; }
}
```

### Project Structure

```
src/
├── ApiPosture/           # CLI application
├── ApiPosture.Core/      # Core analysis engine
└── ApiPosture.Rules/     # Security rules

tests/
├── ApiPosture.Core.Tests/
└── ApiPosture.Rules.Tests/
```

## Adding New Security Rules

Security rules live in `src/ApiPosture.Rules/`. To add a new rule:

### 1. Create the Rule Class

Create a new file in the appropriate category folder:

```csharp
// src/ApiPosture.Rules/YourCategory/AP009YourRuleName.cs
namespace ApiPosture.Rules.YourCategory;

public class AP009YourRuleName : ISecurityRule
{
    public string Id => "AP009";
    public string Name => "Your Rule Name";
    public string Description => "What this rule detects";
    public Severity Severity => Severity.Medium;
    public string Category => "YourCategory";

    public IEnumerable<Finding> Analyze(Endpoint endpoint)
    {
        // Your detection logic here
        if (/* violation detected */)
        {
            yield return new Finding
            {
                RuleId = Id,
                RuleName = Name,
                Severity = Severity,
                Message = "Explanation of the issue",
                Endpoint = endpoint
            };
        }
    }
}
```

### 2. Register the Rule

Add your rule to `RuleEngine.cs`:

```csharp
private static readonly ISecurityRule[] _rules =
[
    // ... existing rules
    new AP009YourRuleName(),
];
```

### 3. Add Tests

Create tests in `tests/ApiPosture.Rules.Tests/`:

```csharp
public class AP009YourRuleNameTests
{
    private readonly AP009YourRuleName _rule = new();

    [Fact]
    public void Should_Detect_Violation()
    {
        // Arrange
        var endpoint = new Endpoint { /* ... */ };

        // Act
        var findings = _rule.Analyze(endpoint).ToList();

        // Assert
        findings.Should().ContainSingle()
            .Which.RuleId.Should().Be("AP009");
    }

    [Fact]
    public void Should_Not_Flag_Valid_Endpoint()
    {
        // Test that valid patterns don't trigger the rule
    }
}
```

### 4. Update Documentation

Add your rule to the table in `README.md`.

## Testing

### Running Tests

```bash
# Run all tests
dotnet test

# Run with verbose output
dotnet test --verbosity normal

# Run specific test project
dotnet test tests/ApiPosture.Rules.Tests

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Writing Tests

We use xUnit and FluentAssertions:

```csharp
using FluentAssertions;
using Xunit;

public class MyTests
{
    [Fact]
    public void Should_Do_Something()
    {
        // Arrange
        var input = "test";

        // Act
        var result = MyMethod(input);

        // Assert
        result.Should().Be("expected");
    }

    [Theory]
    [InlineData("input1", "expected1")]
    [InlineData("input2", "expected2")]
    public void Should_Handle_Multiple_Cases(string input, string expected)
    {
        var result = MyMethod(input);
        result.Should().Be(expected);
    }
}
```

### Testing with Code Fixtures

Use inline C# code for testing analysis:

```csharp
var code = """
    [ApiController]
    public class TestController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get() => Ok();
    }
    """;
var tree = SourceFileLoader.ParseText(code);
var endpoints = _discoverer.Discover(tree).ToList();
```

## Questions?

- **General questions**: Open a [Discussion](https://github.com/BlagoCuljak/ApiPosture/discussions) (if enabled)
- **Bug reports**: Use the [Bug Report](https://github.com/BlagoCuljak/ApiPosture/issues/new?template=bug_report.md) template
- **Feature ideas**: Use the [Feature Request](https://github.com/BlagoCuljak/ApiPosture/issues/new?template=feature_request.md) template

Thank you for contributing to ApiPosture!
