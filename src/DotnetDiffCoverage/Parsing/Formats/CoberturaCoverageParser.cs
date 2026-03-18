using System.Xml;

namespace DotnetDiffCoverage.Parsing.Formats;

/// <summary>
/// Parses a Cobertura XML coverage file into a normalized CoverageResult.
/// Includes lines where hits > 0.
/// </summary>
public sealed class CoberturaCoverageParser : ICoverageFormatParser
{
    public CoverageFormat Format => CoverageFormat.Cobertura;

    public CoverageResult Parse(string filePath)
    {
        var doc = new XmlDocument();
        doc.Load(filePath);

        var result = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);

        var classNodes = doc.SelectNodes("//class");
        if (classNodes is null)
            return CoverageResult.Empty;

        foreach (XmlNode classNode in classNodes)
        {
            var filename = classNode.Attributes?["filename"]?.Value;
            if (string.IsNullOrEmpty(filename))
                continue;

            var normalizedPath = NormalizePath(filename);

            if (!result.TryGetValue(normalizedPath, out var coveredLines))
            {
                coveredLines = new HashSet<int>();
                result[normalizedPath] = coveredLines;
            }

            var lineNodes = classNode.SelectNodes("lines/line");
            if (lineNodes is null)
                continue;

            foreach (XmlNode lineNode in lineNodes)
            {
                var numberStr = lineNode.Attributes?["number"]?.Value;
                var hitsStr = lineNode.Attributes?["hits"]?.Value;

                if (int.TryParse(numberStr, out var number) &&
                    int.TryParse(hitsStr, out var hits) &&
                    hits > 0)
                {
                    coveredLines.Add(number);
                }
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
