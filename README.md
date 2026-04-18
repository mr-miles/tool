# dotnet-diff-coverage

A .NET global tool that cross-references a PR diff with your test coverage report to surface lines you added but didn't test.

Pipe it into CI to block merges when new code lacks coverage, or run it locally before pushing to catch gaps early.  

## Install

```shell
dotnet tool install --global dotnet-diff-coverage
```

Or from a local package:

```shell
dotnet tool install --global dotnet-diff-coverage --add-source ./nupkg
```

## Quick start

```shell
# 1. Generate a unified diff for the PR (or a local branch)
git diff origin/main...HEAD > pr.patch

# 2. Collect coverage (using dotnet-coverage or coverlet)
dotnet-coverage collect "dotnet test" -f cobertura -o coverage.xml

# 3. Run the tool
dotnet-diff-coverage --diff pr.patch --coverage coverage.xml --coverage-format cobertura
```

## CLI options

| Option | Description | Default |
|--------|-------------|---------|
| `--diff <file>` | Path to a unified diff (`.patch`) file. Use `-` to read from stdin. | — |
| `--coverage <file...>` | One or more coverage report files. Accepts Cobertura XML, OpenCover XML, or LCOV. | — |
| `--coverage-format <fmt>` | Coverage file format: `cobertura`, `opencover`, or `lcov`. Required when `--coverage` is provided. | — |
| `--coverage-path-prefix <prefix>` | Prefix to strip from coverage file paths before matching against diff paths. Use this when coverage paths are absolute (e.g. `/home/ci/repo/`) and diff paths are relative (e.g. `src/Foo.cs`). | — |
| `--threshold <pct>` | Maximum allowed percentage of uncovered diff lines (0–100). Exit code 1 when exceeded. | `0` |
| `--output-json <file>` | Write a JSON report to this path. Use `-` for stdout. | — |
| `--output-sarif <file>` | Write a SARIF 2.1.0 report to this path (for GitHub / Azure DevOps annotations). | — |
| `--config <file>` | Path to a JSON or YAML config file. CLI arguments win on conflict. | `dotnet-diff-coverage.json` |
| `--no-color` | Suppress ANSI colour codes in console output. | — |

## Coverage formats

| Format | Produced by | `--coverage-format` value |
|--------|-------------|--------------------------|
| Cobertura XML | `dotnet-coverage`, `coverlet` | `cobertura` |
| OpenCover XML | `OpenCover`, `dotnet-coverage` | `opencover` |
| LCOV | `coverlet`, many Linux tools | `lcov` |

## Matching diff paths to coverage paths

Diff paths are always repo-relative (`src/Services/Foo.cs`). Coverage paths depend on the tool and environment:

- **Cobertura from `coverlet`** — typically repo-relative; exact match works with no extra flags.
- **OpenCover / `dotnet-coverage` on Windows CI** — absolute paths like `C:\agent\_work\1\s\src\Services\Foo.cs`.

When paths don't match exactly, pass `--coverage-path-prefix` with the absolute prefix to strip:

```shell
dotnet-diff-coverage \
  --diff pr.patch \
  --coverage coverage.xml \
  --coverage-format opencover \
  --coverage-path-prefix "C:/agent/_work/1/s/"
```

After stripping the prefix, `C:/agent/_work/1/s/src/Services/Foo.cs` becomes `src/Services/Foo.cs` and matches the diff path exactly.

## Exit codes

| Code | Meaning |
|------|---------|
| `0` | Success — coverage at or above threshold |
| `1` | Coverage failure — uncovered percentage exceeds `--threshold` |
| `2` | Input error — missing or unreadable file, unknown format, etc. |

## Worked example

### Setup

You have a branch that adds a new `OrderService` and fixes a bug in `PriceCalculator`. You want to ensure the PR doesn't introduce untested code.

```
src/
  Services/
    OrderService.cs     ← new file
  Domain/
    PriceCalculator.cs  ← bug fix
```

### Step 1 — Get the diff

```shell
git diff origin/main...feature/order-service > pr.patch
```

`pr.patch` (excerpt):

```diff
--- /dev/null
+++ b/src/Services/OrderService.cs
@@ -0,0 +1,18 @@
+namespace MyApp.Services;
+
+public class OrderService(IOrderRepository repo)
+{
+    public Order GetOrder(int id) => repo.FindById(id);
+
+    public Order PlaceOrder(Cart cart)
+    {
+        var order = Order.FromCart(cart);
+        repo.Save(order);
+        return order;
+    }
+
+    public void CancelOrder(int id)
+    {
+        var order = repo.FindById(id);
+        order.Cancel();
+        repo.Save(order);
+    }
+}
--- a/src/Domain/PriceCalculator.cs
+++ b/src/Domain/PriceCalculator.cs
@@ -12,7 +12,7 @@
     public decimal Calculate(IEnumerable<LineItem> items)
     {
-        return items.Sum(i => i.Price);
+        return items.Sum(i => i.Price * i.Quantity);
     }
```

### Step 2 — Run your tests and collect coverage

```shell
dotnet test --collect "XPlat Code Coverage" --results-directory ./TestResults
# Coverlet writes Cobertura XML to TestResults/*/coverage.cobertura.xml
```

### Step 3 — Run dotnet-diff-coverage

```shell
dotnet-diff-coverage \
  --diff pr.patch \
  --coverage TestResults/*/coverage.cobertura.xml \
  --coverage-format cobertura \
  --threshold 20
```

### Step 4 — Read the output

```
dotnet-diff-coverage — PR diff coverage report
═══════════════════════════════════════════════

src/Services/OrderService.cs
  Lines added:    18
  Lines covered:  7     (GetOrder + PlaceOrder tested)
  Lines uncovered: 11
    → 15: public void CancelOrder(int id)
    → 16: {
    → 17:     var order = repo.FindById(id);
    → 18:     order.Cancel();
    → 19:     repo.Save(order);
    → 20: }
    (and 5 more...)

src/Domain/PriceCalculator.cs
  Lines added:    1
  Lines covered:  1
  Lines uncovered: 0

═══════════════════════════════════════════════
Total added:      19
Total uncovered:  11
Uncovered %:      57.9 %   [threshold: 20 %]

FAILED — uncovered percentage (57.9%) exceeds threshold (20%)
```

Exit code `1` — CI pipeline blocks the merge.

The developer adds tests for `CancelOrder`, re-runs, and the tool exits with `0`.

### Optional: write a SARIF report for GitHub annotations

```shell
dotnet-diff-coverage \
  --diff pr.patch \
  --coverage coverage.cobertura.xml \
  --coverage-format cobertura \
  --output-sarif results.sarif
```

Upload `results.sarif` in your GitHub Actions workflow:

```yaml
- name: Run diff coverage
  run: |
    dotnet-diff-coverage \
      --diff pr.patch \
      --coverage coverage.cobertura.xml \
      --coverage-format cobertura \
      --threshold 20 \
      --output-sarif diff-coverage.sarif

- name: Upload SARIF
  uses: github/codeql-action/upload-sarif@v3
  with:
    sarif_file: diff-coverage.sarif
```

Each uncovered line appears as an inline annotation on the PR.

## Building from source

```shell
git clone https://github.com/your-org/dotnet-diff-coverage
cd dotnet-diff-coverage
dotnet build
dotnet test
dotnet pack src/DotnetDiffCoverage
```

## License

MIT
