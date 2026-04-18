using DotnetDiffCoverage.Analysis;
using System.Text;

namespace DotnetDiffCoverage.Output;

public sealed class ConsoleReporter(bool noColor)
{
    private const string Reset = "[0m";
    private const string Red = "[31m";
    private const string Green = "[32m";
    private const string Yellow = "[33m";
    private const string Cyan = "[36m";
    private const string Bold = "[1m";

    public void Write(CrossReferenceResult result, TextWriter? writer = null)
    {
        writer ??= Console.Out;
        var sb = new StringBuilder();

        if (result.Files.Count == 0 && result.TotalAddedLines == 0)
        {
            sb.AppendLine(Color(Green, "No new lines detected in diff."));
            writer.Write(sb);
            return;
        }

        var uncoveredFiles = result.Files.Where(f => f.UncoveredLines.Count > 0).ToList();
        if (uncoveredFiles.Count > 0)
        {
            sb.AppendLine(Color(Bold, "Uncovered lines introduced in this diff:"));
            sb.AppendLine();
            foreach (var file in uncoveredFiles)
            {
                sb.AppendLine(Color(Cyan, $"  {file.FilePath}"));
                foreach (var line in file.UncoveredLines)
                    sb.AppendLine(Color(Red, $"    line {line}"));
            }
            sb.AppendLine();
        }

        var pct = result.UncoveredPercent;
        var pctColor = pct == 0 ? Green : pct < 20 ? Yellow : Red;

        sb.AppendLine(Color(Bold, "Summary:"));
        sb.AppendLine($"  Added lines   : {result.TotalAddedLines}");
        sb.AppendLine($"  Uncovered     : {result.TotalUncoveredLines}");
        sb.AppendLine($"  Coverage gap  : {Color(pctColor, $"{pct:F1}%")}");

        writer.Write(sb);
    }

    private string Color(string code, string text) =>
        noColor ? text : $"{code}{text}{Reset}";
}
