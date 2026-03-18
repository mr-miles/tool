namespace DotnetDiffCoverage.Parsing.Formats;

/// <summary>
/// Parses an LCOV coverage file into a normalized CoverageResult.
/// Reads SF: (source file) and DA: (line,hits) records. Includes lines where hits > 0.
/// </summary>
public sealed class LcovCoverageParser : ICoverageFormatParser
{
    public CoverageFormat Format => CoverageFormat.Lcov;

    public CoverageResult Parse(string filePath)
    {
        var result = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);
        string? currentFile = null;
        HashSet<int>? currentLines = null;

        foreach (var rawLine in File.ReadLines(filePath))
        {
            var line = rawLine.TrimEnd();

            if (line.StartsWith("SF:", StringComparison.Ordinal))
            {
                currentFile = NormalizePath(line[3..]);
                if (!result.TryGetValue(currentFile, out currentLines))
                {
                    currentLines = new HashSet<int>();
                    result[currentFile] = currentLines;
                }
            }
            else if (line.StartsWith("DA:", StringComparison.Ordinal) &&
                     currentLines is not null)
            {
                // DA:<line>,<hits>[,<checksum>]
                var parts = line[3..].Split(',');
                if (parts.Length >= 2 &&
                    int.TryParse(parts[0], out var lineNum) &&
                    int.TryParse(parts[1], out var hits) &&
                    hits > 0)
                {
                    currentLines.Add(lineNum);
                }
            }
            else if (line.Equals("end_of_record", StringComparison.OrdinalIgnoreCase))
            {
                currentFile = null;
                currentLines = null;
            }
        }

        if (result.Count == 0)
            return CoverageResult.Empty;

        var immutable = new Dictionary<string, IReadOnlySet<int>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (path, lines) in result)
            immutable[path] = lines;

        return new CoverageResult(immutable);
    }

    private static string NormalizePath(string path) =>
        path.Replace('\\', '/');
}
