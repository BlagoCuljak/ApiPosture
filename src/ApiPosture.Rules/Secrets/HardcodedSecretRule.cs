using System.Text.RegularExpressions;
using ApiPosture.Core.Models;
using HttpMethod = ApiPosture.Core.Models.HttpMethod;

namespace ApiPosture.Rules.Secrets;

/// <summary>
/// AP201: Detects hardcoded secrets matching a small set of high-precision, vendor-specific formats.
/// Generic heuristics (password=, token=, connection strings, JWTs) are intentionally not included.
/// </summary>
public sealed class HardcodedSecretRule
{
    public const string RuleId = "AP201";
    public const string Name = "Hardcoded secret";
    public const Severity DefaultSeverity = Severity.High;

    private const long MaxFileSizeBytes = 1_000_000;
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    private sealed record SecretPattern(string Name, Regex Regex);

    private static Regex R(string pattern) => new(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, RegexTimeout);

    private static readonly SecretPattern[] Patterns =
    [
        new("AWS access key ID", R(@"\b(AKIA|ASIA)[0-9A-Z]{16}\b")),
        new("GCP API key", R(@"\bAIza[0-9A-Za-z\-_]{35}\b")),
        new("GitHub token", R(@"\b(ghp|gho|ghu|ghs|ghr)_[0-9A-Za-z]{36}\b")),
        new("GitHub fine-grained token", R(@"\bgithub_pat_[0-9A-Za-z_]{82}\b")),
        new("GitLab personal access token", R(@"\bglpat-[0-9A-Za-z\-_]{20,}\b")),
        new("Stripe live secret key", R(@"\b(sk|rk)_live_[0-9A-Za-z]{24,}\b")),
        new("Slack token", R(@"\bxox[baprs]-[0-9A-Za-z\-]{10,}\b")),
        new("Slack webhook URL", R(@"https://hooks\.slack\.com/services/T[0-9A-Z]{8,}/B[0-9A-Z]{8,}/[0-9A-Za-z]{24}")),
        new("SendGrid API key", R(@"\bSG\.[0-9A-Za-z\-_]{22}\.[0-9A-Za-z\-_]{43}\b")),
        new("npm access token", R(@"\bnpm_[0-9A-Za-z]{36}\b")),
        new("DigitalOcean token", R(@"\bdop_v1_[0-9a-f]{64}\b")),
        new("Private key", R(@"-----BEGIN (RSA |EC |DSA |OPENSSH |PGP |ENCRYPTED )?PRIVATE KEY( BLOCK)?-----")),
    ];

    private static readonly string[] PlaceholderMarkers =
    [
        "EXAMPLE", "XXXX", "your_", "your-", "<", ">", "${", "{{",
        "changeme", "dummy", "fake", "sample", "placeholder", "redacted"
    ];

    private static readonly HashSet<string> ExcludedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "test", "tests", "spec", "fixtures", "testdata", "samples", "examples", "docs",
        "bin", "obj", "node_modules", ".git"
    };

    /// <summary>
    /// Scans a project path (file or directory) and returns AP201 findings.
    /// Scans .cs files, appsettings*.json and .env files.
    /// </summary>
    public IReadOnlyList<Finding> ScanPath(string path)
    {
        var findings = new List<Finding>();
        var root = Directory.Exists(path) ? Path.GetFullPath(path) : Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".";

        foreach (var file in EnumerateCandidateFiles(path, root))
        {
            string content;
            try
            {
                if (new FileInfo(file).Length > MaxFileSizeBytes) continue;
                content = File.ReadAllText(file);
            }
            catch (Exception)
            {
                continue;
            }

            findings.AddRange(ScanContent(file, content, Path.GetRelativePath(root, file)));
        }

        return findings;
    }

    /// <summary>
    /// Scans file content. <paramref name="relativePath"/> is used for path-based exclusions
    /// (defaults to <paramref name="filePath"/>).
    /// </summary>
    public IReadOnlyList<Finding> ScanContent(string filePath, string content, string? relativePath = null)
    {
        var findings = new List<Finding>();
        if (IsExcludedPath(relativePath ?? filePath) || string.IsNullOrEmpty(content))
            return findings;

        var seen = new HashSet<(int Line, string Type)>();
        var lines = content.Split('\n');

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.Length < 20) continue;

            foreach (var pattern in Patterns)
            {
                Match match;
                try
                {
                    match = pattern.Regex.Match(line);
                }
                catch (RegexMatchTimeoutException)
                {
                    continue;
                }

                // Check all matches on the line; report the first non-placeholder one.
                while (match.Success)
                {
                    if (!IsPlaceholder(match.Value))
                    {
                        if (seen.Add((i + 1, pattern.Name)))
                            findings.Add(CreateFinding(filePath, i + 1, match.Index + 1, pattern.Name, match.Value));
                        break;
                    }
                    match = match.NextMatch();
                }
            }
        }

        return findings;
    }

    /// <summary>
    /// Returns true when the path is in a test/sample/vendor location that should not be scanned.
    /// Only directory segments and the file name are considered.
    /// </summary>
    public static bool IsExcludedPath(string path)
    {
        var segments = path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0) return false;

        for (var i = 0; i < segments.Length - 1; i++)
        {
            var dir = segments[i];
            if (ExcludedDirectories.Contains(dir) ||
                dir.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase) ||
                dir.EndsWith(".Test", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        var fileName = segments[^1];
        return fileName.EndsWith("Tests.cs", StringComparison.Ordinal) ||
               fileName.EndsWith("Test.cs", StringComparison.Ordinal);
    }

    /// <summary>
    /// Masks a secret for display: first 4 characters followed by an ellipsis.
    /// </summary>
    public static string Mask(string secret) =>
        (secret.Length <= 4 ? secret[..Math.Min(secret.Length, 1)] : secret[..4]) + "…";

    private static bool IsPlaceholder(string value)
    {
        foreach (var marker in PlaceholderMarkers)
        {
            if (value.Contains(marker, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static IEnumerable<string> EnumerateCandidateFiles(string path, string root)
    {
        if (File.Exists(path))
        {
            yield return Path.GetFullPath(path);
            yield break;
        }

        if (!Directory.Exists(path)) yield break;

        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var dir = pending.Pop();
            var dirName = Path.GetFileName(dir);
            if (dir != root && (ExcludedDirectories.Contains(dirName) ||
                                dirName.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase) ||
                                dirName.EndsWith(".Test", StringComparison.OrdinalIgnoreCase)))
                continue;

            IEnumerable<string> files;
            IEnumerable<string> subDirs;
            try
            {
                files = Directory.EnumerateFiles(dir).ToList();
                subDirs = Directory.EnumerateDirectories(dir).ToList();
            }
            catch (Exception)
            {
                continue;
            }

            foreach (var file in files)
            {
                if (IsCandidateFile(Path.GetFileName(file)))
                    yield return file;
            }

            foreach (var sub in subDirs)
            {
                // Do not follow symlinked directories (avoid cycles / escaping the scan root).
                try
                {
                    if (new DirectoryInfo(sub).LinkTarget != null) continue;
                }
                catch (Exception)
                {
                    continue;
                }
                pending.Push(sub);
            }
        }
    }

    private static bool IsCandidateFile(string fileName)
    {
        if (fileName.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) return true;
        if (fileName.Equals(".env", StringComparison.OrdinalIgnoreCase) ||
            fileName.StartsWith(".env.", StringComparison.OrdinalIgnoreCase)) return true;
        return fileName.StartsWith("appsettings", StringComparison.OrdinalIgnoreCase) &&
               fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
    }

    private static Finding CreateFinding(string filePath, int line, int column, string type, string secret)
    {
        var location = $"{filePath}:{line}";
        var endpoint = new Endpoint
        {
            Route = location,
            Methods = HttpMethod.None,
            Type = EndpointType.MinimalApi,
            Location = new SourceLocation(filePath, line, column),
            Authorization = new AuthorizationInfo(),
            Classification = SecurityClassification.Public
        };

        return new Finding
        {
            RuleId = RuleId,
            RuleName = Name,
            Severity = DefaultSeverity,
            Endpoint = endpoint,
            Message = $"Hardcoded {type} detected at {Path.GetFileName(filePath)}:{line} ({Mask(secret)}).",
            Recommendation = "Remove the secret from source, rotate it immediately, and load it from environment " +
                             "variables or a secret manager instead."
        };
    }
}
