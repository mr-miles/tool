using DotnetDiffCoverage.Models;

namespace DotnetDiffCoverage.Analysis;

/// <summary>
/// Per-file result of cross-referencing a diff against coverage data.
/// </summary>
/// <param name="FilePath">Normalized file path from the diff (no a/ or b/ prefix).</param>
/// <param name="AddedRanges">Consecutive ranges of lines added in the diff for this file.</param>
/// <param name="UncoveredRanges">Ranges of added lines that have no coverage hit. Empty when fully covered.</param>
public sealed record UncoveredFile(
    string FilePath,
    IReadOnlyList<LineRange> AddedRanges,
    IReadOnlyList<LineRange> UncoveredRanges)
{
    /// <summary>Flat list of all added line numbers. Computed from <see cref="AddedRanges"/>.</summary>
    public IReadOnlyList<int> AddedLines => AddedRanges.SelectMany(r => r.Lines).OrderBy(l => l).ToList();

    /// <summary>Flat list of uncovered added line numbers. Computed from <see cref="UncoveredRanges"/>.</summary>
    public IReadOnlyList<int> UncoveredLines => UncoveredRanges.SelectMany(r => r.Lines).OrderBy(l => l).ToList();
}
