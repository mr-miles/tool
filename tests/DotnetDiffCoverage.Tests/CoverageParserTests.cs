using DotnetDiffCoverage.Parsing;
using FluentAssertions;
using Xunit;

namespace DotnetDiffCoverage.Tests;

public class CoverageParserTests
{
    private static CoverageParser CreateParser() => new(
        new CoberturaCoverageParser(),
        new OpenCoverCoverageParser(),
        new LcovCoverageParser());

    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    // ─── Unknown format returns Empty (no exception) ──────────────────────────

    [Fact]
    public void Parse_UnknownFormat_ReturnsEmptyWithoutException()
    {
        var parser = CreateParser();
        var result = parser.Parse(FixturePath("sample-cobertura.xml"), CoverageFormat.Unknown);
        result.FileCoveredLines.Should().BeEmpty();
    }

    // ─── Cobertura parsing ────────────────────────────────────────────────────

    [Fact]
    public void Parse_Cobertura_ExtractsCoveredLines()
    {
        var parser = CreateParser();
        var result = parser.Parse(FixturePath("sample-cobertura.xml"), CoverageFormat.Cobertura);

        result.FileCoveredLines.Should().ContainKey("src/Calculator.cs");
        // hits > 0: lines 5, 6, 7 (lines 8, 9 have hits=0)
        result.FileCoveredLines["src/Calculator.cs"].Should().Contain(5).And.Contain(6).And.Contain(7);
        result.FileCoveredLines["src/Calculator.cs"].Should().NotContain(8).And.NotContain(9);
    }

    [Fact]
    public void Parse_Cobertura_ExcludesZeroHitLines()
    {
        var parser = CreateParser();
        var result = parser.Parse(FixturePath("sample-cobertura.xml"), CoverageFormat.Cobertura);

        // Helper.cs: lines 3, 4 covered; line 5 has hits=0
        result.FileCoveredLines.Should().ContainKey("src/Helper.cs");
        result.FileCoveredLines["src/Helper.cs"].Should().Contain(3).And.Contain(4);
        result.FileCoveredLines["src/Helper.cs"].Should().NotContain(5);
    }

    // ─── OpenCover parsing ────────────────────────────────────────────────────

    [Fact]
    public void Parse_OpenCover_ExtractsCoveredLines()
    {
        var parser = CreateParser();
        var result = parser.Parse(FixturePath("sample-opencover.xml"), CoverageFormat.OpenCover);

        // Calculator.cs: sl=10,11,12 have vc>0; sl=13 has vc=0
        result.FileCoveredLines.Should().ContainKey("C:/src/Calculator.cs");
        result.FileCoveredLines["C:/src/Calculator.cs"].Should()
            .Contain(10).And.Contain(11).And.Contain(12);
        result.FileCoveredLines["C:/src/Calculator.cs"].Should().NotContain(13);
    }

    [Fact]
    public void Parse_OpenCover_ResolvesFilePathViaUid()
    {
        var parser = CreateParser();
        var result = parser.Parse(FixturePath("sample-opencover.xml"), CoverageFormat.OpenCover);

        // Helper.cs: sl=20 has vc=1 (covered); sl=21 has vc=0
        result.FileCoveredLines.Should().ContainKey("C:/src/Helper.cs");
        result.FileCoveredLines["C:/src/Helper.cs"].Should().Contain(20);
        result.FileCoveredLines["C:/src/Helper.cs"].Should().NotContain(21);
    }

    // ─── LCOV parsing ─────────────────────────────────────────────────────────

    [Fact]
    public void Parse_Lcov_ExtractsCoveredLines()
    {
        var parser = CreateParser();
        var result = parser.Parse(FixturePath("sample-lcov.info"), CoverageFormat.Lcov);

        // Calculator.cs: DA:5,3 DA:6,3 DA:7,1 are covered; DA:8,0 DA:9,0 are not
        result.FileCoveredLines.Should().ContainKey("src/Calculator.cs");
        result.FileCoveredLines["src/Calculator.cs"].Should()
            .Contain(5).And.Contain(6).And.Contain(7);
        result.FileCoveredLines["src/Calculator.cs"].Should()
            .NotContain(8).And.NotContain(9);
    }

    [Fact]
    public void Parse_Lcov_HandlesMultipleSourceFiles()
    {
        var parser = CreateParser();
        var result = parser.Parse(FixturePath("sample-lcov.info"), CoverageFormat.Lcov);

        result.FileCoveredLines.Should().ContainKey("src/Helper.cs");
        result.FileCoveredLines["src/Helper.cs"].Should().Contain(3).And.Contain(4);
        result.FileCoveredLines["src/Helper.cs"].Should().NotContain(5);
    }

    // ─── Path normalization (backslashes → forward slashes) ───────────────────

    [Fact]
    public void Parse_OpenCover_NormalizesBackslashPaths()
    {
        var parser = CreateParser();
        var result = parser.Parse(FixturePath("sample-opencover.xml"), CoverageFormat.OpenCover);

        // OpenCover fixture uses C:\src\... — paths must be normalized to forward slashes
        result.FileCoveredLines.Keys.Should().NotContain(k => k.Contains('\\'));
    }
}
