using ApiPosture.Rules.Secrets;
using FluentAssertions;

namespace ApiPosture.Rules.Tests.Secrets;

public class HardcodedSecretRuleTests : IDisposable
{
    private readonly HardcodedSecretRule _rule = new();
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "ap201-" + Guid.NewGuid().ToString("N"));

    public HardcodedSecretRuleTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { /* best effort */ }
    }

    // Secrets are assembled at runtime so no literal secret appears in the repository.
    private static string Rand(int n, string alphabet = "aB3dE5gH7jK9mN2pQ4sT6vW8yZ1cF0hLrUxOiIo")
    {
        var chars = new char[n];
        for (var i = 0; i < n; i++) chars[i] = alphabet[(i * 7 + 3) % alphabet.Length];
        return new string(chars);
    }

    public static TheoryData<string, string> TruePositives => new()
    {
        { "AWS access key ID", "AK" + "IA" + "7QZMBN4TR2XK5PLW" },
        { "AWS access key ID", "AS" + "IA" + "7QZMBN4TR2XK5PLW" },
        { "GCP API key", "AI" + "za" + Rand(35) },
        { "GitHub token", "gh" + "p_" + Rand(36) },
        { "GitHub token", "gh" + "o_" + Rand(36) },
        { "GitHub token", "gh" + "u_" + Rand(36) },
        { "GitHub token", "gh" + "s_" + Rand(36) },
        { "GitHub token", "gh" + "r_" + Rand(36) },
        { "GitHub fine-grained token", "github" + "_pat_" + Rand(82) },
        { "GitLab personal access token", "gl" + "pat-" + Rand(20) },
        { "Stripe live secret key", "sk" + "_live_" + Rand(26) },
        { "Stripe live secret key", "rk" + "_live_" + Rand(26) },
        { "Slack token", "xo" + "xb-" + "1234567890-" + Rand(12) },
        { "Slack webhook URL", "https://hooks." + "slack.com/services/" + "T0123ABCD9/B0123ABCD9/" + Rand(24) },
        { "SendGrid API key", "S" + "G." + Rand(22) + "." + Rand(43) },
        { "npm access token", "np" + "m_" + Rand(36) },
        { "DigitalOcean token", "do" + "p_v1_" + new string('a', 32) + new string('7', 32) },
        { "Private key", "-----BEGIN " + "RSA PRIVATE KEY-----" },
        { "Private key", "-----BEGIN " + "PRIVATE KEY-----" },
        { "Private key", "-----BEGIN " + "OPENSSH PRIVATE KEY-----" },
        { "Private key", "-----BEGIN PGP " + "PRIVATE KEY BLOCK-----" },
    };

    [Theory]
    [MemberData(nameof(TruePositives))]
    public void Detects_KnownSecretFormats(string type, string secret)
    {
        var code = $"namespace App;\nclass C {{\n    const string K = \"{secret}\";\n}}\n";

        var findings = _rule.ScanContent("/proj/Config.cs", code, "Config.cs");

        findings.Should().ContainSingle();
        var f = findings[0];
        f.RuleId.Should().Be("AP201");
        f.Severity.Should().Be(ApiPosture.Core.Models.Severity.High);
        f.Message.Should().Contain(type);
        f.Endpoint.Location.LineNumber.Should().Be(3);
        f.Endpoint.Location.FilePath.Should().Be("/proj/Config.cs");
    }

    [Fact]
    public void NeverPrintsFullSecret()
    {
        var secret = "gh" + "p_" + Rand(36);
        var findings = _rule.ScanContent("/proj/A.cs", $"var t = \"{secret}\";", "A.cs");

        findings.Should().ContainSingle();
        var f = findings[0];
        (f.Message + f.Recommendation + f.Endpoint.Route).Should().NotContain(secret);
        f.Message.Should().Contain("ghp_…");
        HardcodedSecretRule.Mask(secret).Should().Be("ghp_…");
    }

    [Fact]
    public void OneFindingPerFileLineAndType()
    {
        var a = "gh" + "p_" + Rand(36);
        var b = "gh" + "o_" + Rand(36);
        var findings = _rule.ScanContent("/p/A.cs", $"var x = \"{a}\" + \"{b}\";\nvar y = \"{a}\";", "A.cs");

        findings.Should().HaveCount(2); // line 1 (one GitHub token type), line 2
        findings.Select(f => f.Endpoint.Location.LineNumber).Should().BeEquivalentTo([1, 2]);
    }

    [Fact]
    public void DifferentTypesOnSameLine_AreReportedSeparately()
    {
        var aws = "AK" + "IA" + "7QZMBN4TR2XK5PLW";
        var gh = "gh" + "p_" + Rand(36);
        var findings = _rule.ScanContent("/p/A.cs", $"var x = \"{aws}\" + \"{gh}\";", "A.cs");

        findings.Should().HaveCount(2);
    }

    [Theory]
    [InlineData("AK" + "IAIOSFODNN7EXAMPLE")]
    [InlineData("AK" + "IA" + "XXXXXXXXXXXXXXXX")]
    [InlineData("gh" + "p_" + "yourtokenyourtokenyourtokenyourtoken1")]
    [InlineData("gh" + "p_" + "fakefakefakefakefakefakefakefakefake")]
    [InlineData("gh" + "p_" + "dummydummydummydummydummydummydummy12")]
    [InlineData("gh" + "p_" + "samplesamplesamplesamplesamplesample12")]
    [InlineData("gh" + "p_" + "placeholderplaceholderplaceholder1234")]
    [InlineData("gh" + "p_" + "changemechangemechangemechangemechang")]
    [InlineData("gh" + "p_" + "redactedredactedredactedredactedredac")]
    [InlineData("sk" + "_live_" + "your_key_hereyour_key_here")]
    [InlineData("sk" + "_live_" + "your-key-hereyour-key-here")]
    public void IgnoresPlaceholderValues(string value)
    {
        var findings = _rule.ScanContent("/p/A.cs", $"var k = \"{value}\";", "A.cs");

        findings.Should().BeEmpty();
    }

    [Theory]
    [InlineData("var k = \"${AWS_KEY}\"; // AKIA placeholder")]
    [InlineData("-----BEGIN <RSA PRIVATE KEY>-----")]
    [InlineData("const k = \"{{ secrets.token }}\";")]
    public void IgnoresTemplatesAndNonMatches(string line)
    {
        _rule.ScanContent("/p/A.cs", line + "\n", "A.cs").Should().BeEmpty();
    }

    [Fact]
    public void IgnoresStripePublishableKey()
    {
        var pk = "pk" + "_live_" + Rand(30);
        _rule.ScanContent("/p/A.cs", $"var k = \"{pk}\";", "A.cs").Should().BeEmpty();
    }

    [Theory]
    [InlineData("password = \"hunter2hunter2\"")]
    [InlineData("var apiKey = \"abcdef0123456789abcdef0123456789\";")]
    [InlineData("var jwt = \"Bearer aaaa.bbbb.cccc\";")]
    [InlineData("var s = \"Server=db;Database=x;User Id=sa;Password=hunter2hunter2;\";")]
    public void IgnoresGenericPatterns(string line)
    {
        _rule.ScanContent("/p/A.cs", line, "A.cs").Should().BeEmpty();
    }

    [Theory]
    [InlineData("tests/Foo.cs")]
    [InlineData("test/Foo.cs")]
    [InlineData("src/spec/Foo.cs")]
    [InlineData("fixtures/Foo.cs")]
    [InlineData("testdata/Foo.cs")]
    [InlineData("samples/Foo.cs")]
    [InlineData("examples/Foo.cs")]
    [InlineData("docs/Foo.cs")]
    [InlineData("src/bin/Debug/Foo.cs")]
    [InlineData("src/obj/Foo.cs")]
    [InlineData("node_modules/x/Foo.cs")]
    [InlineData("src/MyApp.Tests/Foo.cs")]
    [InlineData("src/FooTests.cs")]
    [InlineData("src/FooTest.cs")]
    public void IgnoresTestAndVendorPaths(string relative)
    {
        HardcodedSecretRule.IsExcludedPath(relative).Should().BeTrue();
        var code = "var t = \"" + "gh" + "p_" + Rand(36) + "\";";

        _rule.ScanContent("/p/" + relative, code, relative).Should().BeEmpty();
    }

    [Theory]
    [InlineData("src/Config.cs")]
    [InlineData("Program.cs")]
    [InlineData("src/Contest/Config.cs")]
    [InlineData("src/Latest.cs")]
    public void DoesNotExcludeOrdinaryPaths(string relative)
    {
        HardcodedSecretRule.IsExcludedPath(relative).Should().BeFalse();
    }

    [Fact]
    public void ScanPath_FindsCsAppsettingsAndEnv_AndSkipsExcludedDirs()
    {
        var token = "gh" + "p_" + Rand(36);
        Directory.CreateDirectory(Path.Combine(_tempDir, "src"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "tests"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "Foo.Tests"));
        File.WriteAllText(Path.Combine(_tempDir, "src", "A.cs"), $"var t = \"{token}\";");
        File.WriteAllText(Path.Combine(_tempDir, "appsettings.Production.json"), $"{{ \"Token\": \"{token}\" }}");
        File.WriteAllText(Path.Combine(_tempDir, ".env"), $"TOKEN={token}");
        File.WriteAllText(Path.Combine(_tempDir, "readme.txt"), token);
        File.WriteAllText(Path.Combine(_tempDir, "tests", "B.cs"), $"var t = \"{token}\";");
        File.WriteAllText(Path.Combine(_tempDir, "Foo.Tests", "C.cs"), $"var t = \"{token}\";");
        File.WriteAllText(Path.Combine(_tempDir, "src", "DTests.cs"), $"var t = \"{token}\";");

        var findings = _rule.ScanPath(_tempDir);

        findings.Select(f => Path.GetFileName(f.Endpoint.Location.FilePath))
            .Should().BeEquivalentTo(["A.cs", "appsettings.Production.json", ".env"]);
    }

    [Fact]
    public void ScanPath_RootInsideExcludedNamedDirectory_StillScans()
    {
        // The exclusion applies to paths relative to the scan root, not to the root itself.
        var root = Path.Combine(_tempDir, "samples", "App");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "A.cs"), $"var t = \"{"gh" + "p_" + Rand(36)}\";");

        _rule.ScanPath(root).Should().ContainSingle();
    }
}
