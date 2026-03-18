# Phase 3: Coverage Parsing — Context

## Phase Goal
Auto-detect coverage file format (Cobertura, OpenCover, LCOV) and parse into a normalized file → covered-lines model that the cross-reference engine (Phase 4) will consume.

## Requirements Covered
- **REQ-05**: Coverage format selection — The user specifies the format via `--coverage-format cobertura|opencover|lcov` on the CLI. No file-content sniffing. Invalid value exits with code 2.
- **REQ-06**: Coverage file parsing — Parse the specified coverage format into a normalized model: `file path → set of covered line numbers` (lines with hits > 0).

## What Already Exists (from Phases 1 & 2)
- `DotnetDiffCoverage.sln` — solution file
- `Directory.Build.props` — TreatWarningsAsErrors=true, net8.0, LangVersion=latest
- `src/DotnetDiffCoverage/DotnetDiffCoverage.csproj` — CLI project
- `src/DotnetDiffCoverage/Commands/RootCommandBuilder.cs` — 7 options already defined; `--coverage-format` to be added
- `src/DotnetDiffCoverage/Parsing/DiffParser.cs`, `DiffResult.cs`, `DiffFile.cs` — diff parsing (Parsing namespace)
- `src/DotnetDiffCoverage/Services/ServiceRegistration.cs` — DI registration, currently has AddTransient<DiffParser>()
- `tests/DotnetDiffCoverage.Tests/` — xUnit + FluentAssertions; 23 tests passing

## Key Design Decisions
- **Namespace**: `DotnetDiffCoverage.Parsing` — all parsers live in `src/DotnetDiffCoverage/Parsing/`
- **Normalized model**: `CoverageResult` wraps `IReadOnlyDictionary<string, IReadOnlySet<int>>` — file path → set of covered line numbers. Keys are normalized (backslashes → forward slashes).
- **Format selection**: User provides `--coverage-format` on the CLI. `CoverageParser.Parse(string filePath, CoverageFormat format)` accepts the pre-resolved format enum; no file-content detection.
- **Parser interface**: `ICoverageFormatParser` with `CoverageResult Parse(string filePath)`.
- **Orchestrator**: `CoverageParser.Parse(string filePath, CoverageFormat format)` — selects parser based on format, returns `CoverageResult`. Returns `CoverageResult.Empty` for `Unknown` (defensive fallback only).
- **CLI option**: `--coverage-format` added to `RootCommandBuilder.cs` as `Option<string?>` with documented values `cobertura`, `opencover`, `lcov`. Required validation is deferred to the handler (Phase 5).
- **Cobertura**: XML — `<class filename="..."><lines><line number="N" hits="H"/></lines></class>`. Include line if hits > 0.
- **OpenCover**: XML — `<SequencePoint vc="N" sl="L" fileid="F"/>` with `<File uid="F" fullPath="..."/>`. Include if vc > 0.
- **LCOV**: Line-based — `SF:path` sets current file; `DA:line,hits` records covered lines (hits > 0).
- **Path normalization**: `NormalizePath(string)` converts backslashes to forward slashes.
- **DI**: `CoverageParser`, `CoberturaCoverageParser`, `OpenCoverCoverageParser`, `LcovCoverageParser` registered as transient in `ServiceRegistration.cs`. No `CoverageFormatDetector`.
- **Architecture proposals**: Skipped by user.
- **Spec pipeline**: Skipped.

## Plan Structure
- **Plan 03-01 (Wave 1)**: `--coverage-format` CLI option, coverage models, three format parsers, orchestrator (format-param API), DI registration — Agent: engineering-senior-developer
- **Plan 03-02 (Wave 2)**: Fixture coverage files (3 formats) + `CoverageParserTests.cs` — Agent: engineering-senior-developer
