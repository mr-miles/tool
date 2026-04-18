using Microsoft.Extensions.FileSystemGlobbing;

namespace DotnetDiffCoverage.Analysis;

/// <summary>
/// Filters files from a diff result that match configured test file glob patterns.
/// Test files are excluded from uncovered-line analysis — we care about untested
/// production code, not untested test helpers.
/// </summary>
public sealed class TestFileFilter
{
    /// <summary>
    /// Returns a new DiffResult with test files removed.
    /// </summary>
    public static DotnetDiffCoverage.Parsing.DiffResult ExcludeTestFiles(
        DotnetDiffCoverage.Parsing.DiffResult diff,
        IEnumerable<string> testPatterns)
    {
        var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
        foreach (var pattern in testPatterns)
            matcher.AddInclude(pattern);

        var filtered = diff.FileAddedLines
            .Where(kvp => !IsTestFile(kvp.Key, matcher))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        return new DotnetDiffCoverage.Parsing.DiffResult(
            (IReadOnlyDictionary<string, IReadOnlyList<int>>)filtered);
    }

    private static bool IsTestFile(string filePath, Matcher matcher)
    {
        // Matcher works with relative paths; normalise separators
        var normalised = filePath.Replace('\', '/').TrimStart('/');
        return matcher.Match(normalised).HasMatches;
    }
}
