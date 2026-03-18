# dotnet-diff-coverage

## What This Is
A CLI and CI tool that cross-references a code diff with .NET coverage files to surface exactly which lines introduced by the diff lack test coverage. Given a diff (from a file or a PR API) and one or more coverage reports, it identifies uncovered new code and reports it in formats suitable for both humans and pipelines.

## Core Value
Closes the gap between "we have coverage" and "our new code is covered." Instead of tracking an aggregate percentage, teams can see precisely which lines in a PR or commit are untested — making coverage gaps visible, actionable, and enforceable in CI.

## Who It's For
- .NET developers running it locally before opening a PR
- Engineering teams enforcing coverage quality gates on every PR via CI pipelines (GitHub Actions, Azure DevOps)

## Requirements

### Validated
(None yet — ship to validate)

### Active
- REQ-01: CLI entry point — Tool is invokable via `dotnet tool run dotnet-diff-coverage` with documented arguments for all inputs and outputs
- REQ-02: NuGet packaging — Project is packaged as a .NET Tool publishable to NuGet.org (`dotnet tool install`)
- REQ-03: Unified diff parsing — Parse a unified diff file or string to extract per-file added/modified line numbers
- REQ-04: PR API diff fetch — Fetch diff from a GitHub PR or Azure DevOps PR given a URL or ID and an API token
- REQ-05: Coverage format auto-detection — Detect Cobertura, OpenCover, and LCOV from file contents without requiring a user-specified format flag
- REQ-06: Coverage file parsing — Parse the detected coverage format into a normalized model: file path → set of covered line numbers
- REQ-07: Cross-reference engine — Intersect diff added/modified lines with the coverage model to produce a list of uncovered diff lines per file
- REQ-08: Console summary output — Write a human-readable report of uncovered lines to stdout
- REQ-09: Exit code enforcement — Exit non-zero when uncovered diff lines are detected (configurable threshold)
- REQ-10: JSON report output — Write a structured JSON report to a configurable output path
- REQ-11: SARIF output — Write a SARIF 2.1.0 report for GitHub/ADO inline annotation integration
- REQ-12: Config file support — Read settings (input paths, thresholds, output paths, API tokens) from a .json or .yml config file

### Out of Scope
- Running tests or generating coverage files (tool consumes existing coverage output only)
- IDE / Visual Studio / Rider extension
- Branch coverage or path coverage analysis (line coverage only for v1)
- Multi-language support (.NET projects only)

## Constraints
- Written in C# targeting .NET 8 or later
- No external services — all analysis runs locally; PR API calls are explicit and optional
- Distributed via NuGet as a dotnet global/local tool

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| dotnet tool packaging | Standard distribution path for .NET CLI tools; zero-friction install via `dotnet tool install` | Ship as NuGet package |
| User-specified coverage format | Simpler and more explicit than file-content sniffing; no ambiguity when files have unexpected headers | `--coverage-format cobertura\|opencover\|lcov` CLI flag; invalid value exits with code 2 |
| SARIF + JSON output | SARIF is natively understood by GitHub and ADO for inline PR annotations; JSON enables downstream tooling | Both formats required for v1 |
| PR API integration | Eliminates manual diff export step in CI pipelines | Support GitHub and Azure DevOps PR APIs |

## Architecture Influences
- Core analysis pipeline: diff parser → coverage parser → cross-reference engine → output formatters
- Each stage produces a normalized intermediate model (diff lines, coverage model, uncovered lines) so parsers and formatters are independently swappable
- CLI uses System.CommandLine for argument parsing and `IHost` for DI
- Coverage parsing should support streaming/lazy loading for large coverage files

---
*Last updated: 2026-03-18 after initialization*
