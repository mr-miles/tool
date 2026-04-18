using Microsoft.Extensions.FileSystemGlobbing;

namespace DotnetDiffCoverage.Analysis;

/// <summary>
/// Filters files from a diff result that match configured test file or exclude glob patterns.
/// Test files are excluded from uncovered-line analysis — we care about untested
/// production code, not untested test helpers.
/// Non-source files (e.g. .yml, .json, .md) are excluded entirely since they cannot
/// have coverage data and would always appear as uncovered.
/// </summary>
public sealed class TestFileFilter
{
    /// <summary>
    /// Returns a new DiffResult with test files and excluded files removed.
    /// </summary>
    public static DotnetDiffCoverage.Parsing.DiffResult ExcludeTestFiles(
        DotnetDiffCoverage.Parsing.DiffResult diff,
        IEnumerable<string> testPatterns,
        IEnumerable<string>? excludePatterns = null)
    {
        var testMatcher = new Matcher(StringComparison.OrdinalIgnoreCase);
        foreach (var pattern in testPatterns)
            testMatcher.AddInclude(pattern);

        Matcher? excludeMatcher = null;
        if (excludePatterns != null)
        {
            excludeMatcher = new Matcher(StringComparison.OrdinalIgnoreCase);
            foreach (var pattern in excludePatterns)
                excludeMatcher.AddInclude(pattern);
        }

        var filtered = diff.FileAddedLines
            .Where(kvp => !IsTestFile(kvp.Key, testMatcher)
                       && !IsExcluded(kvp.Key, excludeMatcher))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        return new DotnetDiffCoverage.Parsing.DiffResult(
            (IReadOnlyDictionary<string, IReadOnlyList<int>>)filtered);
    }

    private static bool IsTestFile(string filePath, Matcher matcher)
    {
        // Matcher works with relative paths; normalise separators
        var normalised = filePath.Replace('\\', '/').TrimStart('/');
        return matcher.Match(normalised).HasMatches;
    }

    private static bool IsExcluded(string filePath, Matcher? matcher)
    {
        if (matcher == null)
            return false;

        var normalised = filePath.Replace('\\', '/').TrimStart('/');
        return matcher.Match(normalised).HasMatches;
    }
}
