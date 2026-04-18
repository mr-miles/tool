using System.Text.Json;
using System.Text.Json.Serialization;
using DotnetDiffCoverage.Analysis;
using System.Reflection;

namespace DotnetDiffCoverage.Output;

/// <summary>Generates SARIF 2.1.0 output for GitHub Code Scanning / PR annotations.</summary>
public sealed class SarifReporter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly string ToolVersion =
        typeof(SarifReporter).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion.Split('+')[0] ?? "0.0.0";

    public async Task WriteAsync(CrossReferenceResult result, Stream stream)
    {
        var results = result.Files
            .SelectMany(f => f.UncoveredLines.Select(line => new
            {
                ruleId = "DC001",
                level = "warning",
                message = new { text = $"Line {line} was added but is not covered by any test." },
                locations = new[]
                {
                    new
                    {
                        physicalLocation = new
                        {
                            artifactLocation = new
                            {
                                uri = f.FilePath.Replace('\', '/'),
                                uriBaseId = "%SRCROOT%",
                            },
                            region = new { startLine = line },
                        },
                    },
                },
            }))
            .ToList();

        var sarif = new
        {
            version = "2.1.0",
            schema = "https://json.schemastore.org/sarif-2.1.0.json",
            runs = new[]
            {
                new
                {
                    tool = new
                    {
                        driver = new
                        {
                            name = "dotnet-diff-coverage",
                            version = ToolVersion,
                            informationUri = "https://github.com/mr-miles/tool",
                            rules = new[]
                            {
                                new
                                {
                                    id = "DC001",
                                    name = "UncoveredAddedLine",
                                    shortDescription = new { text = "Added line not covered by tests" },
                                    fullDescription = new { text = "A line was added in this diff but is not covered by any test in the provided coverage reports." },
                                    helpUri = "https://github.com/mr-miles/tool#dc001",
                                    defaultConfiguration = new { level = "warning" },
                                },
                            },
                        },
                    },
                    results,
                },
            },
        };

        await JsonSerializer.SerializeAsync(stream, sarif, Options);
    }
}
