using DotnetDiffCoverage.Models;

namespace DotnetDiffCoverage.Parsing;

/// <summary>
/// Intermediate representation of a single file's diff data, built up during parsing.
/// </summary>
internal sealed class DiffFile
{
    public string FilePath { get; set; } = string.Empty;

    /// <summary>Consecutive ranges of added lines, in order of appearance.</summary>
    public List<LineRange> AddedRanges { get; } = new();

    /// <summary>Flat list of added line numbers (computed from ranges). For backwards compatibility.</summary>
    public IReadOnlyList<int> AddedLines => AddedRanges.SelectMany(r => r.Lines).OrderBy(l => l).ToList();
}
