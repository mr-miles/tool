# Requirements

Full requirement descriptions for cross-referencing from ROADMAP.md phase details.

---

## REQ-01: CLI Entry Point
**Summary**: Invokable via `dotnet tool run dotnet-diff-coverage` with documented arguments.

The tool must expose a well-documented CLI surface using `System.CommandLine`. Required arguments: `--diff` (path to diff file or stdin), `--coverage` (one or more coverage file paths), `--output-json` (optional JSON report path), `--output-sarif` (optional SARIF report path), `--threshold` (optional max uncovered percentage before failing), `--config` (optional config file path). Must print `--help` with all options. Must support being piped to (stdin diff input).

---

## REQ-02: NuGet Packaging
**Summary**: Packaged as a .NET Tool publishable to NuGet.org.

The project's `.csproj` must include the standard dotnet tool packaging properties: `PackAsTool`, `ToolCommandName`, `PackageId`, `Version`, `Description`, `Authors`. Must produce a `.nupkg` on `dotnet pack`. Must be installable locally via `dotnet tool install --add-source ./nupkg dotnet-diff-coverage` and globally via NuGet once published.

---

## REQ-03: Unified Diff Parsing
**Summary**: Parse a unified diff file/string to extract per-file added/modified line numbers.

Given a unified diff (git diff output or a `.patch` file), produce a mapping of `filename → list of added line numbers`. Only lines with a `+` prefix (additions) count — removed lines and context lines are ignored. Must handle multi-file diffs, renames (`--- a/ +++ b/`), binary file markers (skip gracefully), and diffs with no changes. File paths should be normalized (strip `a/` and `b/` prefixes).

---

## REQ-04: PR API Diff Fetch
**Summary**: Fetch diff from GitHub PR or Azure DevOps PR given URL/ID and token.

Supports two providers:
- **GitHub**: Given a PR URL (`https://github.com/{owner}/{repo}/pull/{number}`) or owner/repo/PR-number, fetch the diff using the GitHub REST API (`application/vnd.github.diff` media type) with a PAT.
- **Azure DevOps**: Given a PR URL or org/project/repo/PR-ID, fetch the diff using the ADO REST API with a PAT.

Output is a unified diff string fed into the same parser as REQ-03. Tokens are read from CLI args, config file, or environment variables (`GITHUB_TOKEN`, `ADO_TOKEN`).

---

## REQ-05: Coverage Format Selection
**Summary**: The user specifies the coverage format via `--coverage-format` on the CLI.

Accepted values (case-insensitive): `cobertura`, `opencover`, `lcov`.

The `--coverage-format` option is required when `--coverage` files are provided. If the value does not match a known format, the tool exits with code 2 and a clear error message listing the accepted values. No file-content sniffing is performed — the format is entirely determined by the CLI argument.

---

## REQ-06: Coverage File Parsing
**Summary**: Parse detected format into normalized model: file path → set of covered line numbers.

Normalized model: `Dictionary<string, HashSet<int>>` where keys are source file paths and values are line numbers with at least one coverage hit (count > 0).

- **Cobertura**: Read `<line number="N" hits="H"/>` under each `<class filename="...">`. Include line if `hits > 0`.
- **OpenCover**: Read `<SequencePoint vc="N" sl="L"/>` under each `<FileRef uid="...">`. Include if `vc > 0`.
- **LCOV**: Read `DA:line,hits` records. Include if hits > 0. `SF:` marks the current file.

File path normalization: convert backslashes to forward slashes, make relative to a common root if possible.

---

## REQ-07: Cross-Reference Engine
**Summary**: Intersect diff added lines with coverage model → list of uncovered diff lines per file.

Algorithm:
1. For each file in the diff added-lines map (REQ-03 / REQ-04 output)
2. Normalize the file path to match coverage model keys (try exact match, then suffix match)
3. For each added line number: check if it's in the covered set for that file
4. Collect uncovered lines per file
5. Compute aggregate stats: total added lines, total uncovered lines, uncovered percentage

File path matching strategy: if exact match fails, match by longest common suffix (handles different root prefixes between diff and coverage).

---

## REQ-08: Console Summary Output
**Summary**: Write a human-readable report of uncovered lines to stdout.

Format:
```
dotnet-diff-coverage report
===========================
Files analyzed : 5
Lines added    : 42
Lines uncovered: 8 (19.0%)

Uncovered lines:
  src/Foo/Bar.cs        : 12, 15, 16
  src/Baz/Qux.cs        : 7

Result: FAIL (threshold: 0%)
```
Supports `--no-color` flag for CI environments that don't handle ANSI codes. When all diff lines are covered, print `Result: PASS`.

---

## REQ-09: Exit Code Enforcement
**Summary**: Exit non-zero when uncovered diff lines exceed the configured threshold.

Default threshold: 0% (any uncovered diff line = failure). Configurable via `--threshold N` where N is the maximum allowed uncovered percentage (0–100). Exit codes:
- `0` — All diff lines covered (or uncovered % ≤ threshold)
- `1` — Uncovered lines found above threshold
- `2` — Input error (missing file, parse failure, invalid args)

---

## REQ-10: JSON Report Output
**Summary**: Write a structured JSON report to a configurable output path.

Schema:
```json
{
  "summary": {
    "totalAddedLines": 42,
    "uncoveredLines": 8,
    "uncoveredPercent": 19.0,
    "threshold": 0,
    "passed": false
  },
  "files": [
    {
      "path": "src/Foo/Bar.cs",
      "addedLines": [10, 12, 15, 16],
      "uncoveredLines": [12, 15, 16]
    }
  ]
}
```
Written to the path specified by `--output-json`. If path is `-`, write to stdout.

---

## REQ-11: SARIF Output
**Summary**: Write a SARIF 2.1.0 report for GitHub/ADO inline annotation integration.

Each uncovered diff line becomes a SARIF `result` with:
- `ruleId`: `"DCT001"` (diff-coverage: uncovered line)
- `message`: `"Line added in diff is not covered by tests."`
- `locations`: physical location with file URI and line number
- `level`: `"warning"`

SARIF file is valid per the SARIF 2.1.0 schema. Written to `--output-sarif` path. Compatible with GitHub Actions' `upload-sarif` action and ADO's publish code analysis task.

---

## REQ-12: Config File Support
**Summary**: Read settings from a .json or .yml config file.

Default config file search: `dotnet-diff-coverage.json` then `dotnet-diff-coverage.yml` in the current directory. Override with `--config` flag. CLI arguments take precedence over config file values.

Config file schema (JSON example):
```json
{
  "diff": "path/to/diff.patch",
  "coverage": ["coverage1.xml", "coverage2.xml"],
  "outputJson": "reports/coverage-diff.json",
  "outputSarif": "reports/coverage-diff.sarif",
  "threshold": 10,
  "github": { "token": "...", "pr": "https://github.com/..." },
  "ado": { "token": "...", "pr": "https://..." }
}
```
