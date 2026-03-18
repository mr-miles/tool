namespace DotnetDiffCoverage.Parsing;

/// <summary>
/// Parses a coverage file of a specific known format into a normalized CoverageResult.
/// </summary>
public interface ICoverageFormatParser
{
    /// <summary>The coverage format this parser handles.</summary>
    CoverageFormat Format { get; }

    CoverageResult Parse(string filePath);
}
