using System.Xml;

namespace DotnetDiffCoverage.Parsing.Formats;

/// <summary>
/// Parses an OpenCover XML coverage file into a normalized CoverageResult.
/// Includes sequence points where vc (visit count) > 0.
/// </summary>
public sealed class OpenCoverCoverageParser : ICoverageFormatParser
{
    public CoverageFormat Format => CoverageFormat.OpenCover;

    public CoverageResult Parse(string filePath)
    {
        var doc = new XmlDocument();
        doc.Load(filePath);

        // Build uid → normalized file path map from all File elements
        var fileMap = new Dictionary<string, string>(StringComparer.Ordinal);
        var fileNodes = doc.SelectNodes("//File");
        if (fileNodes is not null)
        {
            foreach (XmlNode fileNode in fileNodes)
            {
                var uid = fileNode.Attributes?["uid"]?.Value;
                var fullPath = fileNode.Attributes?["fullPath"]?.Value;
                if (!string.IsNullOrEmpty(uid) && !string.IsNullOrEmpty(fullPath))
                    fileMap[uid] = NormalizePath(fullPath);
            }
        }

        var result = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);

        var spNodes = doc.SelectNodes("//SequencePoint");
        if (spNodes is null)
            return CoverageResult.Empty;

        foreach (XmlNode sp in spNodes)
        {
            var vcStr = sp.Attributes?["vc"]?.Value;
            var slStr = sp.Attributes?["sl"]?.Value;
            var fileid = sp.Attributes?["fileid"]?.Value;

            if (!int.TryParse(vcStr, out var vc) || vc <= 0)
                continue;
            if (!int.TryParse(slStr, out var sl))
                continue;
            if (fileid is null || !fileMap.TryGetValue(fileid, out var normalizedPath))
                continue;

            if (!result.TryGetValue(normalizedPath, out var coveredLines))
            {
                coveredLines = new HashSet<int>();
                result[normalizedPath] = coveredLines;
            }

            coveredLines.Add(sl);
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
