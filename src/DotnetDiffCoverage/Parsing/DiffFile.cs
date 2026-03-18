namespace DotnetDiffCoverage.Parsing;

/// <summary>
/// Intermediate representation of a single file's diff data, built up during parsing.
/// </summary>
internal sealed class DiffFile
{
    public string FilePath { get; set; } = string.Empty;
    public List<int> AddedLines { get; } = new();
}
