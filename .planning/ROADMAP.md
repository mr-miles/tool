# dotnet-diff-coverage — Roadmap

## Phases

- [ ] Phase 1: Foundation & CLI Scaffolding
- [ ] Phase 2: Diff Parsing
- [ ] Phase 3: Coverage Parsing
- [ ] Phase 4: Cross-Reference Engine
- [ ] Phase 5: Output & Reporting
- [ ] Phase 6: PR API Integration
- [ ] Phase 7: Config & CI Integration

## Phase Details

### Phase 1: Foundation & CLI Scaffolding
**Goal**: Establish the solution structure, CLI entry point, DI container, and NuGet tool packaging so all subsequent phases have a runnable, testable host to build on.
**Requirements**: REQ-01, REQ-02
**Recommended Agents**: engineering-senior-developer, testing-evidence-collector
**Success Criteria**:
- `dotnet build` succeeds with zero warnings
- `dotnet pack` produces a `.nupkg` with `PackAsTool=true`
- `dotnet tool install --add-source ./nupkg dotnet-diff-coverage` installs successfully
- `dotnet-diff-coverage --help` prints all expected arguments
- Unit test project exists and `dotnet test` passes (even with 0 tests)
**Plans**: 2

---

### Phase 2: Diff Parsing
**Goal**: Parse a unified diff (from a file or stdin) and extract per-file added/modified line numbers into a normalized model.
**Requirements**: REQ-03
**Recommended Agents**: engineering-senior-developer, testing-api-tester
**Success Criteria**:
- Given a multi-file unified diff, parser returns correct file → added-lines mapping
- Renames, binary markers, and empty diffs are handled without errors
- `a/` and `b/` path prefixes are stripped from output
- Unit tests cover happy path, renames, binary files, empty diff
**Plans**: 2

---

### Phase 3: Coverage Parsing
**Goal**: Auto-detect coverage file format (Cobertura, OpenCover, LCOV) and parse into a normalized file → covered-lines model.
**Requirements**: REQ-05, REQ-06
**Recommended Agents**: engineering-senior-developer, testing-api-tester
**Success Criteria**:
- Auto-detection correctly identifies all three formats from file headers
- Unknown formats produce a warning and are skipped (no crash)
- Parsed coverage model correctly reflects covered lines (hits > 0) for all three formats
- Unit tests cover each format with representative fixture files
**Plans**: 2

---

### Phase 4: Cross-Reference Engine
**Goal**: Intersect the diff added-lines map with the coverage model to produce uncovered diff lines per file, with aggregate statistics.
**Requirements**: REQ-07
**Recommended Agents**: engineering-senior-developer, testing-api-tester
**Success Criteria**:
- Engine correctly identifies uncovered lines when diff and coverage share files
- File path matching handles prefix differences (diff uses `a/b/` paths, coverage uses absolute paths)
- Aggregate stats (total added, total uncovered, uncovered %) are computed correctly
- Unit tests cover exact match, suffix match, fully covered, fully uncovered, and empty diff scenarios
**Plans**: 2

---

### Phase 5: Output & Reporting
**Goal**: Implement all output formatters — console summary, exit code logic, JSON report, and SARIF 2.1.0 report.
**Requirements**: REQ-08, REQ-09, REQ-10, REQ-11
**Recommended Agents**: engineering-senior-developer, testing-evidence-collector
**Success Criteria**:
- Console output matches documented format; `--no-color` suppresses ANSI codes
- Exit code is 0 on pass, 1 on coverage failure, 2 on input error
- JSON report matches documented schema and is valid JSON
- SARIF report is valid per SARIF 2.1.0 schema; each uncovered line is a `result`
- End-to-end test with a known diff + coverage file produces correct outputs
**Plans**: 3

---

### Phase 6: PR API Integration
**Goal**: Implement GitHub and Azure DevOps PR API clients that fetch a unified diff given a PR URL or ID, feeding the existing diff parser.
**Requirements**: REQ-04
**Recommended Agents**: engineering-backend-architect, testing-api-tester
**Success Criteria**:
- GitHub client fetches diff from a real PR using `application/vnd.github.diff` media type
- ADO client fetches diff from a real PR using the ADO REST API
- Tokens are read from CLI args, config file, and env vars (`GITHUB_TOKEN`, `ADO_TOKEN`)
- Invalid URLs or missing tokens produce clear error messages (exit code 2)
- Integration tests (or recorded HTTP fixtures) cover both providers
**Plans**: 2

---

### Phase 7: Config & CI Integration
**Goal**: Add config file support and provide CI integration artifacts (GitHub Actions workflow, Azure DevOps pipeline task example, README documentation).
**Requirements**: REQ-12
**Recommended Agents**: engineering-devops-automator, product-technical-writer
**Success Criteria**:
- Config file is auto-discovered (`dotnet-diff-coverage.json` / `.yml`) and merged with CLI args (CLI wins on conflict)
- GitHub Actions example workflow runs the tool and uploads SARIF
- Azure DevOps pipeline example runs the tool and publishes annotations
- README documents all CLI arguments, config schema, and both CI integration examples
**Plans**: 2

---

## Progress

| Phase | Plans | Completed | Status |
|-------|-------|-----------|--------|
| 1: Foundation & CLI Scaffolding | 2 | 2 | Complete |
| 2: Diff Parsing | 2 | 0 | Pending |
| 3: Coverage Parsing | 2 | 0 | Pending |
| 4: Cross-Reference Engine | 2 | 0 | Pending |
| 5: Output & Reporting | 3 | 0 | Pending |
| 6: PR API Integration | 2 | 0 | Pending |
| 7: Config & CI Integration | 2 | 0 | Pending |
