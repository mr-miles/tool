using System.Text.Json;
using System.Text.Json.Serialization;
using DotnetDiffCoverage.Analysis;

namespace DotnetDiffCoverage.Output;

public sealed class JsonReporter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task WriteAsync(CrossReferenceResult result, Stream stream)
    {
        if (result == null)
        {
            return;
        }
        
        var report = new
        {
            summary = new
            {
                totalAddedLines = result.TotalAddedLines,
                totalUncoveredLines = result.TotalUncoveredLines,
                uncoveredPercentage = Math.Round(result.UncoveredPercent, 2),
            },
            uncoveredFiles = result.Files
                .Where(f => f.UncoveredLines.Count > 0)
                .Select(f => new
                {
                    path = f.FilePath,
                    uncoveredLines = f.UncoveredLines,
                    uncoveredRanges = f.UncoveredRanges
                        .Select(r => new { start = r.Start, end = r.End })
                        .ToList(),
                }),
        };
        await JsonSerializer.SerializeAsync(stream, report, Options);
    }
}
