using DotnetDiffCoverage.Models;
using DotnetDiffCoverage.Parsing;

namespace DotnetDiffCoverage.Analysis;

/// <summary>
/// Intersects a <see cref="DiffResult"/> with a <see cref="CoverageResult"/> to produce
/// a <see cref="CrossReferenceResult"/> listing uncovered diff ranges per file with aggregate statistics.
/// </summary>
public sealed class CrossReferenceEngine
{
    /// <summary>
    /// Analyzes which lines added in the diff are not covered by the provided coverage data.
    /// </summary>
    /// <param name="diff">Parsed diff result.</param>
    /// <param name="coverage">Parsed coverage result.</param>
    /// <param name="coveragePathPrefix">
    /// Optional prefix to strip from coverage file paths before matching against diff paths.
    /// </param>
    public CrossReferenceResult Analyze(DiffResult diff, CoverageResult coverage, string? coveragePathPrefix = null)
    {
        if (diff.FileAddedRanges.Count == 0)
            return CrossReferenceResult.Empty;

        var files = new List<UncoveredFile>(diff.FileAddedRanges.Count);

        foreach (var (diffPath, addedRanges) in diff.FileAddedRanges)
        {
            var coveredLines = FindCoverageMatch(diffPath, coverage, coveragePathPrefix);

            IReadOnlyList<LineRange> uncoveredRanges = coveredLines is null
                ? addedRanges
                : GroupUncoveredLines(addedRanges, coveredLines);

            files.Add(new UncoveredFile(diffPath, addedRanges, uncoveredRanges));
        }

        var totalAdded = files.Sum(f => f.AddedLines.Count);
        var totalUncovered = files.Sum(f => f.UncoveredLines.Count);
        var uncoveredPercent = totalAdded == 0
            ? 0.0
            : (double)totalUncovered / totalAdded * 100.0;

        return new CrossReferenceResult(files, totalAdded, totalUncovered, uncoveredPercent);
    }

    /// <summary>
    /// Takes the added ranges for a file and the set of covered lines, and returns the uncovered
    /// lines grouped back into <see cref="LineRange"/> objects.
    /// </summary>
    private static IReadOnlyList<LineRange> GroupUncoveredLines(
        IReadOnlyList<LineRange> addedRanges, IReadOnlySet<int> coveredLines)
    {
        var result = new List<LineRange>();
        int? runStart = null, runEnd = null;

        foreach (var line in addedRanges.SelectMany(r => r.Lines).OrderBy(l => l))
        {
            if (coveredLines.Contains(line))
            {
                if (runStart.HasValue)
                {
                    result.Add(new LineRange(runStart.Value, runEnd!.Value));
                    runStart = null;
                    runEnd = null;
                }
            }
            else
            {
                if (runStart == null)
                {
                    runStart = line;
                    runEnd = line;
                }
                else if (line == runEnd + 1)
                {
                    runEnd = line;
                }
                else
                {
                    result.Add(new LineRange(runStart.Value, runEnd!.Value));
                    runStart = line;
                    runEnd = line;
                }
            }
        }

        if (runStart.HasValue)
            result.Add(new LineRange(runStart.Value, runEnd!.Value));

        return result;
    }

    /// <summary>
    /// Finds the coverage entry that matches <paramref name="diffPath"/>.
    /// Returns null when no match is found (file treated as fully uncovered).
    /// </summary>
    private static IReadOnlySet<int>? FindCoverageMatch(
        string diffPath, CoverageResult coverage, string? coveragePathPrefix)
    {
        if (coverage.FileCoveredLines.TryGetValue(diffPath, out var exact))
            return exact;

        if (coveragePathPrefix is not null)
        {
            var normalizedPrefix = coveragePathPrefix.Replace('\\', '/');
            if (!normalizedPrefix.EndsWith('/'))
                normalizedPrefix += '/';

            foreach (var (coveragePath, lines) in coverage.FileCoveredLines)
            {
                var normalizedCoverage = coveragePath.Replace('\\', '/');
                if (normalizedCoverage.StartsWith(normalizedPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    var stripped = normalizedCoverage[normalizedPrefix.Length..];
                    if (string.Equals(stripped, diffPath, StringComparison.OrdinalIgnoreCase))
                        return lines;
                }
            }
        }

        return null;
    }
}
