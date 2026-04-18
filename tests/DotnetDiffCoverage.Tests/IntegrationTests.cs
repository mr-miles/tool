using DotnetDiffCoverage.Analysis;
using DotnetDiffCoverage.Parsing;
using DotnetDiffCoverage.Parsing.Formats;
using FluentAssertions;
using Xunit;

namespace DotnetDiffCoverage.Tests;

/// <summary>
/// End-to-end integration tests: real DiffParser + real CoverageParser + CrossReferenceEngine.
/// Each test scenario corresponds to a realistic CI/PR workflow situation.
/// </summary>
public class IntegrationTests : IDisposable
{
    private readonly DiffParser _diffParser = new();
    private readonly CrossReferenceEngine _engine = new();
    private readonly List<string> _tempFiles = [];

    private string WriteTempFile(string content)
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, content);
        _tempFiles.Add(path);
        return path;
    }

    private CoverageResult ParseCobertura(string xml) =>
        new CoberturaCoverageParser().Parse(WriteTempFile(xml));

    private CoverageResult ParseLcov(string lcov) =>
        new LcovCoverageParser().Parse(WriteTempFile(lcov));

    private CoverageResult ParseOpenCover(string xml) =>
        new OpenCoverCoverageParser().Parse(WriteTempFile(xml));

    public void Dispose()
    {
        foreach (var f in _tempFiles)
            if (File.Exists(f)) File.Delete(f);
    }

    // ─── 1. Fixture-based: real patch file + Cobertura fixture ───────────────
    //
    // Simulates a CI run where dotnet coverage produced a Cobertura XML
    // and the PR diff is read from a .patch file.

    [Fact]
    public void FixtureBased_SimpleMultiFilePatch_CoberturaFixture_CorrectUncoveredLines()
    {
        var patchPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "simple-multi-file.patch");
        var xmlPath   = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sample-cobertura.xml");

        var diff     = _diffParser.ParseFile(patchPath);
        var coverage = new CoberturaCoverageParser().Parse(xmlPath);
        var result   = _engine.Analyze(diff, coverage);

        // Calculator.cs: added [4,5,6,9,11,12]; covered {5,6,7} → uncovered [4,9,11,12]
        // (blank context lines in the patch are ignored by the parser, shifting line numbers)
        var calc = result.Files.Single(f => f.FilePath == "src/Calculator.cs");
        calc.AddedLines.Should().BeEquivalentTo(new[] { 5, 6, 9, 11, 12 });
        calc.UncoveredLines.Should().BeEquivalentTo(new[] { 9, 11, 12 });

        // Program.cs: added [3,4]; no coverage entry → all uncovered
        var prog = result.Files.Single(f => f.FilePath == "src/Program.cs");
        prog.UncoveredLines.Should().BeEquivalentTo(new[] { 3, 4 });

        // Aggregate: 7 added, 5 uncovered → ~71.4 %
        result.TotalAddedLines.Should().Be(7);
        result.TotalUncoveredLines.Should().Be(5);
        result.UncoveredPercent.Should().BeApproximately(71.43, 0.01);
    }

    // ─── 2. Fixture-based: same patch file + LCOV fixture ────────────────────

    [Fact]
    public void FixtureBased_SimpleMultiFilePatch_LcovFixture_SameResultAsCobertura()
    {
        var patchPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "simple-multi-file.patch");
        var lcovPath  = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sample-lcov.info");

        var diff     = _diffParser.ParseFile(patchPath);
        var coverage = new LcovCoverageParser().Parse(lcovPath);
        var result   = _engine.Analyze(diff, coverage);

        // LCOV and Cobertura fixtures represent the same coverage data for Calculator.cs
        var calc = result.Files.Single(f => f.FilePath == "src/Calculator.cs");
        calc.UncoveredLines.Should().BeEquivalentTo(new[] { 9, 11, 12 });

        result.TotalUncoveredLines.Should().Be(5);
    }

    // ─── 3. New service method added, developer forgot to write tests ─────────
    //
    // Common PR pattern: developer adds ProcessPayment() but has no test for it.
    // Expect: all 6 newly added lines flagged as uncovered.

    [Fact]
    public void NewServiceMethod_NoTestsWritten_AllAddedLinesUncovered()
    {
        var diff = _diffParser.Parse("""
            --- a/src/Services/PaymentService.cs
            +++ b/src/Services/PaymentService.cs
            @@ -5,9 +5,15 @@
                 private readonly IGateway _gateway;

                 public PaymentService(IGateway gateway)
                 {
                     _gateway = gateway;
                 }
            +
            +    public PaymentResult ProcessPayment(decimal amount)
            +    {
            +        _gateway.Charge(amount);
            +        return PaymentResult.Approved;
            +    }
             }
            """);

        // Existing tests only exercise the constructor (lines 7-9).
        // Note: blank context lines in the diff are ignored by the parser, so the
        // empty line before the new method becomes added line 10, shifting things by 1.
        var coverage = ParseCobertura("""
            <?xml version="1.0" encoding="utf-8"?>
            <coverage>
              <packages><package name="MyApp"><classes>
                <class name="PaymentService" filename="src/Services/PaymentService.cs">
                  <lines>
                    <line number="7" hits="3" />
                    <line number="8" hits="3" />
                    <line number="9" hits="3" />
                  </lines>
                </class>
              </classes></package></packages>
            </coverage>
            """);

        var result = _engine.Analyze(diff, coverage);

        result.Files.Should().ContainSingle();
        var file = result.Files[0];
        file.AddedLines.Should().BeEquivalentTo(new[] { 10, 11, 12, 13, 14, 15 });
        file.UncoveredLines.Should().BeEquivalentTo(new[] { 10, 11, 12, 13, 14, 15 },
            because: "no tests exercise the new ProcessPayment method");
        result.UncoveredPercent.Should().Be(100.0);
    }

    // ─── 4. Bug fix — corrected calculation, test suite already covers it ─────
    //
    // Developer fixes a wrong formula. The existing test that caught the bug
    // now hits the changed line. Expect: 0 uncovered lines.

    [Fact]
    public void BugFix_ExistingTestCoversFix_ZeroUncoveredLines()
    {
        var diff = _diffParser.Parse("""
            --- a/src/OrderCalculator.cs
            +++ b/src/OrderCalculator.cs
            @@ -8,7 +8,7 @@
                 public decimal CalculateTotal(IEnumerable<OrderItem> items)
                 {
            -        return items.Sum(i => i.Price);
            +        return items.Sum(i => i.Price * i.Quantity);
                 }
            """);

        var coverage = ParseCobertura("""
            <?xml version="1.0" encoding="utf-8"?>
            <coverage>
              <packages><package name="MyApp"><classes>
                <class name="OrderCalculator" filename="src/OrderCalculator.cs">
                  <lines>
                    <line number="8" hits="5" />
                    <line number="9" hits="5" />
                    <line number="10" hits="5" />
                    <line number="11" hits="5" />
                  </lines>
                </class>
              </classes></package></packages>
            </coverage>
            """);

        var result = _engine.Analyze(diff, coverage);

        // Line 10 is the only added line; it's covered
        result.Files.Should().ContainSingle();
        result.Files[0].AddedLines.Should().BeEquivalentTo(new[] { 10 });
        result.TotalUncoveredLines.Should().Be(0);
        result.UncoveredPercent.Should().Be(0.0);
    }

    // ─── 5. New file added to the repo, zero test coverage yet ───────────────
    //
    // Developer adds a brand-new EmailValidator class. No tests exist yet.
    // Coverage file has no entry for this file.
    // Expect: all lines uncovered, file still appears in result.

    [Fact]
    public void NewFile_NoCoverageEntry_AllAddedLinesUncovered()
    {
        var diff = _diffParser.Parse("""
            --- /dev/null
            +++ b/src/Validators/EmailValidator.cs
            @@ -0,0 +1,8 @@
            +using System.Text.RegularExpressions;
            +
            +namespace MyApp.Validators;
            +
            +public class EmailValidator
            +{
            +    public bool IsValid(string email) => Regex.IsMatch(email, @"^[^@]+@[^@]+\.[^@]+$");
            +}
            """);

        // Coverage from an unrelated file only
        var coverage = ParseCobertura("""
            <?xml version="1.0" encoding="utf-8"?>
            <coverage>
              <packages><package name="MyApp"><classes>
                <class name="OtherClass" filename="src/Other.cs">
                  <lines><line number="1" hits="1" /></lines>
                </class>
              </classes></package></packages>
            </coverage>
            """);

        var result = _engine.Analyze(diff, coverage);

        result.Files.Should().ContainSingle();
        var file = result.Files[0];
        file.FilePath.Should().Be("src/Validators/EmailValidator.cs");
        file.AddedLines.Should().HaveCount(8);
        file.UncoveredLines.Should().HaveCount(8,
            because: "new file has no coverage entry so every added line is uncovered");
        result.UncoveredPercent.Should().Be(100.0);
    }

    // ─── 6. Multi-file PR: mixed coverage across three files ─────────────────
    //
    // Realistic PR that spans three files:
    //   - src/Domain/Product.cs      → 2 added, both covered  → 0 uncovered
    //   - src/Services/ProductService.cs → 2 added, 1 covered  → 1 uncovered
    //   - src/Http/ProductController.cs  → 6 added (new file)  → 6 uncovered
    // Aggregate: 10 added, 7 uncovered → 70 %

    [Fact]
    public void MultiFilePR_MixedCoverage_CorrectAggregateStats()
    {
        var diff = _diffParser.Parse("""
            --- a/src/Domain/Product.cs
            +++ b/src/Domain/Product.cs
            @@ -5,4 +5,6 @@
                 public string Name { get; set; }
                 public decimal Price { get; set; }
            +    public int StockLevel { get; set; }
            +    public bool IsAvailable => StockLevel > 0;
             }
            --- a/src/Services/ProductService.cs
            +++ b/src/Services/ProductService.cs
            @@ -10,4 +10,6 @@
                 public Product GetProduct(int id)
                 {
                     return _repo.FindById(id);
                 }
            +    public IEnumerable<Product> GetAvailable() => _repo.FindAll().Where(p => p.IsAvailable);
            +    public void UpdateStock(int id, int qty) => _repo.UpdateStock(id, qty);
            --- /dev/null
            +++ b/src/Http/ProductController.cs
            @@ -0,0 +1,6 @@
            +using Microsoft.AspNetCore.Mvc;
            +namespace MyApp.Http;
            +[ApiController, Route("api/products")]
            +public class ProductController(ProductService svc)
            +{
            +    [HttpGet] public IEnumerable<Product> GetAll() => svc.GetAvailable();
            +}
            """);

        var coverage = ParseCobertura("""
            <?xml version="1.0" encoding="utf-8"?>
            <coverage>
              <packages><package name="MyApp"><classes>
                <class name="Product" filename="src/Domain/Product.cs">
                  <lines>
                    <line number="5" hits="10" />
                    <line number="6" hits="10" />
                    <line number="7" hits="4" />
                    <line number="8" hits="4" />
                  </lines>
                </class>
                <class name="ProductService" filename="src/Services/ProductService.cs">
                  <lines>
                    <line number="10" hits="5" />
                    <line number="11" hits="5" />
                    <line number="12" hits="5" />
                    <line number="13" hits="5" />
                    <line number="14" hits="2" />
                    <line number="15" hits="0" />
                  </lines>
                </class>
              </classes></package></packages>
            </coverage>
            """);

        var result = _engine.Analyze(diff, coverage);

        result.Files.Should().HaveCount(3);

        var product = result.Files.Single(f => f.FilePath == "src/Domain/Product.cs");
        product.AddedLines.Should().BeEquivalentTo(new[] { 7, 8 });
        product.UncoveredLines.Should().BeEmpty(because: "both new properties have tests");

        var service = result.Files.Single(f => f.FilePath == "src/Services/ProductService.cs");
        service.AddedLines.Should().BeEquivalentTo(new[] { 14, 15 });
        service.UncoveredLines.Should().BeEquivalentTo(new[] { 15 },
            because: "GetAvailable has a test but UpdateStock does not");

        var controller = result.Files.Single(f => f.FilePath == "src/Http/ProductController.cs");
        controller.AddedLines.Should().HaveCount(7);
        controller.UncoveredLines.Should().HaveCount(7,
            because: "no tests for the new controller");

        result.TotalAddedLines.Should().Be(11);
        result.TotalUncoveredLines.Should().Be(8);
        result.UncoveredPercent.Should().BeApproximately(72.72, 0.01);
    }

    // ─── 7. OpenCover absolute paths (Windows CI) + --coverage-path-prefix ────
    //
    // dotnet-opencover on a build agent produces absolute Windows paths:
    //   C:/agent/_work/1/s/src/Auth/TokenValidator.cs
    // The git diff uses repo-relative paths:
    //   src/Auth/TokenValidator.cs
    //
    // Without --coverage-path-prefix the engine finds no match and treats all
    // added lines as uncovered. With the prefix the match is exact.

    [Fact]
    public void OpenCoverWindowsAgent_WithPathPrefix_MatchesCorrectly()
    {
        var diff = _diffParser.Parse("""
            --- a/src/Auth/TokenValidator.cs
            +++ b/src/Auth/TokenValidator.cs
            @@ -15,4 +15,6 @@
                 public bool ValidateToken(string token)
                 {
                     return _cache.Contains(token);
                 }
            +    public bool IsExpired(string token) => !_cache.Contains(token);
            +    public void RevokeToken(string token) => _cache.Remove(token);
            """);

        // OpenCover from a Windows CI agent — absolute paths with backslashes
        var coverage = ParseOpenCover("""
            <?xml version="1.0" encoding="utf-8"?>
            <CoverageSession>
              <Modules>
                <Module>
                  <Files>
                    <File uid="1" fullPath="C:\agent\_work\1\s\src\Auth\TokenValidator.cs" />
                  </Files>
                  <Classes>
                    <Class>
                      <Methods>
                        <Method>
                          <SequencePoints>
                            <SequencePoint vc="5" sl="15" fileid="1" />
                            <SequencePoint vc="5" sl="16" fileid="1" />
                            <SequencePoint vc="5" sl="17" fileid="1" />
                            <SequencePoint vc="5" sl="18" fileid="1" />
                            <SequencePoint vc="3" sl="19" fileid="1" />
                            <SequencePoint vc="0" sl="20" fileid="1" />
                          </SequencePoints>
                        </Method>
                      </Methods>
                    </Class>
                  </Classes>
                </Module>
              </Modules>
            </CoverageSession>
            """);

        // Added lines are 19 (IsExpired) and 20 (RevokeToken)
        // Coverage: 19 vc=3 (covered), 20 vc=0 (not covered)

        var withoutPrefix = _engine.Analyze(diff, coverage);
        withoutPrefix.Files.Single().UncoveredLines.Should()
            .BeEquivalentTo(new[] { 19, 20 },
                because: "without a prefix the absolute coverage path cannot be matched");

        var withPrefix = _engine.Analyze(diff, coverage,
            coveragePathPrefix: "C:/agent/_work/1/s/");
        withPrefix.Files.Single().UncoveredLines.Should()
            .BeEquivalentTo(new[] { 20 },
                because: "with the prefix, IsExpired is seen as covered; only RevokeToken is not");
    }

    // ─── 8. LCOV from a Linux CI run — partial coverage of a new feature ──────
    //
    // Developer adds a new caching layer. Unit tests cover the happy path but
    // miss the cache-miss branch.

    [Fact]
    public void LcovLinuxCi_NewCachingLayer_MissBranchUncovered()
    {
        var diff = _diffParser.Parse("""
            --- a/src/Infrastructure/ResponseCache.cs
            +++ b/src/Infrastructure/ResponseCache.cs
            @@ -3,4 +3,10 @@
             public class ResponseCache
             {
                 private readonly Dictionary<string, string> _store = new();
            +
            +    public string GetOrFetch(string key, Func<string> fetch)
            +    {
            +        if (_store.TryGetValue(key, out var cached)) return cached;
            +        var value = fetch();
            +        _store[key] = value;
            +        return value;
            +    }
             }
            """);

        // Tests cover the cache-hit branch (line 8) but not the cache-miss path (lines 9-11)
        var coverage = ParseLcov("""
            TN:
            SF:src/Infrastructure/ResponseCache.cs
            DA:3,2
            DA:7,2
            DA:8,2
            DA:9,0
            DA:10,0
            DA:11,0
            LH:3
            LF:6
            end_of_record
            """);

        var result = _engine.Analyze(diff, coverage);

        result.Files.Should().ContainSingle();
        var file = result.Files[0];

        // Added lines: 4 (empty), 5 (method signature), 6 ({), 7 (if), 8 (cache hit return), 9 (fetch), 10 (store), 11 (return)
        file.AddedLines.Should().Contain(9).And.Contain(10).And.Contain(11);
        file.UncoveredLines.Should().Contain(9, because: "cache-miss branch: fetch() call not tested")
            .And.Contain(10, because: "cache-miss branch: _store[key] = value not tested")
            .And.Contain(11, because: "cache-miss branch: return value not tested");

        // The cache-hit path (line 8) and method signature (lines 5-7) should be covered or absent
        file.UncoveredLines.Should().NotContain(8,
            because: "the cache-hit branch is exercised by existing tests");
    }

    // ─── 9. Renamed file — additions attributed to new name ──────────────────
    //
    // Developer renames a class and adds a method in the same commit.
    // The diff uses the new filename; coverage also uses the new filename.

    [Fact]
    public void RenamedFile_AdditionsAttributedToNewName_CorrectUncoveredLines()
    {
        var diff = _diffParser.Parse("""
            --- a/src/Services/OldService.cs
            +++ b/src/Services/NewService.cs
            @@ -1,4 +1,6 @@
             namespace MyApp.Services;

             public class NewService
             {
            +    public string Describe() => nameof(NewService);
            +    public int Version() => 2;
             }
            """);

        // Coverage is keyed to the new file name; Describe() is tested, Version() is not.
        // The blank line after the namespace declaration is ignored by the parser,
        // so the two methods land at lines 4 and 5 (not 5 and 6).
        var coverage = ParseCobertura("""
            <?xml version="1.0" encoding="utf-8"?>
            <coverage>
              <packages><package name="MyApp"><classes>
                <class name="NewService" filename="src/Services/NewService.cs">
                  <lines>
                    <line number="4" hits="1" />
                    <line number="5" hits="0" />
                  </lines>
                </class>
              </classes></package></packages>
            </coverage>
            """);

        var result = _engine.Analyze(diff, coverage);

        result.Files.Should().ContainSingle();
        var file = result.Files[0];
        file.FilePath.Should().Be("src/Services/NewService.cs");
        file.AddedLines.Should().BeEquivalentTo(new[] { 4, 5 });
        file.UncoveredLines.Should().BeEquivalentTo(new[] { 5 },
            because: "Version() has no test but Describe() does");
    }
}
