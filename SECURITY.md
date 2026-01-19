# Security Policy

## Supported Versions

| Version | Supported          |
| ------- | ------------------ |
| 1.x.x   | :white_check_mark: |

## Reporting a Vulnerability

We take security vulnerabilities seriously. If you discover a security issue, please report it responsibly.

### How to Report

**DO NOT** create a public GitHub issue for security vulnerabilities.

Instead, please report security vulnerabilities by emailing the maintainers directly or using GitHub's private vulnerability reporting:

1. Go to the [Security tab](https://github.com/BlagoCuljak/ApiPosture/security)
2. Click "Report a vulnerability"
3. Fill out the form with details about the vulnerability

### What to Include

Please include the following information:

- Type of vulnerability
- Full path to the affected source file(s)
- Step-by-step instructions to reproduce
- Proof-of-concept or exploit code (if possible)
- Impact assessment

### Response Timeline

- **Acknowledgment**: Within 48 hours
- **Initial Assessment**: Within 1 week
- **Resolution Target**: Within 30 days (depending on complexity)

### What to Expect

1. We will acknowledge receipt of your report
2. We will investigate and validate the issue
3. We will work on a fix and coordinate disclosure
4. We will credit you in the release notes (unless you prefer anonymity)

## Security Best Practices for Users

When using ApiPosture:

1. **Keep Updated**: Always use the latest version
2. **Review Findings**: Security findings are informational - always verify before acting
3. **Secure Your Pipeline**: Store NuGet API keys as secrets, never in code
4. **Validate Sources**: Only install from official NuGet.org

## Scope

This security policy applies to:

- The ApiPosture CLI tool
- The ApiPosture.Core library
- The ApiPosture.Rules library

Out of scope:

- Third-party dependencies (report to those maintainers)
- Issues in the sample projects
- General questions about security (use GitHub Discussions)

Thank you for helping keep ApiPosture secure!
