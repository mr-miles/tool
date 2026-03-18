namespace DotnetDiffCoverage.Parsing;

/// <summary>
/// The normalized result of parsing a unified diff.
/// Maps each modified file path to the list of line numbers added in the diff.
/// </summary>
public sealed class DiffResult
{
    /// <summary>
    /// File path (normalized, no a/ or b/ prefix) → sorted list of added line numbers.
    /// Files with only removals or context changes have no entry here.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<int>> FileAddedLines { get; }

    public DiffResult(IReadOnlyDictionary<string, IReadOnlyList<int>> fileAddedLines)
    {
        FileAddedLines = fileAddedLines;
    }

    /// <summary>Returns a DiffResult with no files — used for empty or no-op diffs.</summary>
    public static DiffResult Empty { get; } =
        new(new Dictionary<string, IReadOnlyList<int>>());
}
