namespace DotnetDiffCoverage.Parsing;

/// <summary>
/// Orchestrating coverage parser: selects the appropriate format parser based on the
/// caller-supplied <see cref="CoverageFormat"/> and delegates parsing to it.
/// Returns <see cref="CoverageResult.Empty"/> for <see cref="CoverageFormat.Unknown"/>.
/// </summary>
public sealed class CoverageParser
{
    private readonly Dictionary<CoverageFormat, ICoverageFormatParser> _parsers;

    public CoverageParser(IEnumerable<ICoverageFormatParser> parsers)
    {
        _parsers = parsers.ToDictionary(p => p.Format);
    }

    /// <summary>
    /// Parses the coverage file using the specified format.
    /// Returns <see cref="CoverageResult.Empty"/> if the format is <see cref="CoverageFormat.Unknown"/>
    /// or no parser is registered for the format.
    /// </summary>
    public CoverageResult Parse(string filePath, CoverageFormat format) =>
        _parsers.TryGetValue(format, out var parser) ? parser.Parse(filePath) : CoverageResult.Empty;
}
