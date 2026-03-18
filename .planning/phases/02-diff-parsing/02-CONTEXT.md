# Phase 2: Diff Parsing — Context

## Phase Goal
Parse a unified diff (from a file or stdin) and extract per-file added/modified line numbers into a normalized model that all later phases consume.

## Requirements Covered
- **REQ-03**: Unified diff parsing — Parse a unified diff file or string to extract per-file added/modified line numbers. Only lines with a `+` prefix (additions) count. Must handle multi-file diffs, renames (`--- a/ +++ b/`), binary file markers (skip gracefully), and diffs with no changes. File paths must be normalized (strip `a/` and `b/` prefixes).

## What Already Exists (from Phase 1)
- `DotnetDiffCoverage.sln` — solution file
- `Directory.Build.props` — shared MSBuild properties (net8.0, TreatWarningsAsErrors=true)
- `src/DotnetDiffCoverage/DotnetDiffCoverage.csproj` — CLI project (System.CommandLine, Microsoft.Extensions.Hosting)
- `src/DotnetDiffCoverage/Program.cs` — entry point (IHost + RootCommandBuilder)
- `src/DotnetDiffCoverage/Commands/RootCommandBuilder.cs` — all 7 CLI options defined (--diff, --coverage, --output-json, --output-sarif, --threshold, --config, --no-color)
- `src/DotnetDiffCoverage/Services/ServiceRegistration.cs` — DI stub for registering services
- `tests/DotnetDiffCoverage.Tests/DotnetDiffCoverage.Tests.csproj` — xUnit + FluentAssertions
- `tests/DotnetDiffCoverage.Tests/CliSmokeTests.cs` — 10 smoke tests all passing
- `dotnet build` and `dotnet test` both pass

## Key Design Decisions
- **Namespace**: `DotnetDiffCoverage.Parsing` — all diff/coverage parsers live in `src/DotnetDiffCoverage/Parsing/`
- **Model**: `DiffResult` is a thin wrapper around `IReadOnlyDictionary<string, IReadOnlyList<int>>` — file path → sorted list of added line numbers. `DiffFile` is an intermediate record used during parsing.
- **Parsing target**: Only `+` prefixed lines count as additions; removed lines (`-`) and context lines (space prefix) are ignored. Header lines (`---`, `+++`, `@@`, `diff --git`) are structural and not counted.
- **File path normalization**: Strip leading `a/` or `b/` from paths extracted from `---` / `+++` headers. For renames, use the `+++` path (new name) as the canonical key.
- **Binary files**: When a `Binary files ... differ` line is encountered, skip that file entirely (no entry in result, no error).
- **Empty/no-op diffs**: Return a `DiffResult` with an empty dictionary — not an error.
- **Input abstraction**: `DiffParser` has two entry points: `Parse(string diffText)` for in-memory strings and `ParseFile(string filePath)` for file-based input. Stdin is handled by the caller reading all of stdin into a string.
- **DI registration**: `DiffParser` registered as transient in `ServiceRegistration.cs`.
- **Architecture proposals**: Skipped by user — pragmatic single-class parser approach.
- **Spec pipeline**: Skipped.

## Plan Structure
- **Plan 02-01 (Wave 1)**: Diff parser model and implementation — Creates `Parsing/DiffFile.cs`, `Parsing/DiffResult.cs`, `Parsing/DiffParser.cs`, and updates `ServiceRegistration.cs`. Agents: engineering-senior-developer + testing-api-tester.
- **Plan 02-02 (Wave 2)**: Unit tests with patch file fixtures — Creates test fixture `.patch` files and `DiffParserTests.cs` with full coverage. Agent: engineering-senior-developer.
