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

    // Anonymous types cannot carry attributes, so we use a record for the top-level
    // SARIF object in order to emit the "$schema" key via [JsonPropertyName].
    private sealed record SarifRoot(
        string Version,
        [property: JsonPropertyName("$schema")] string Schema,
        object[] Runs);

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
                                uri = f.FilePath.Replace('\\', '/'),
                                uriBaseId = "%SRCROOT%",
                            },
                            region = new { startLine = line },
                        },
                    },
                },
            }))
            .ToList();

        var sarif = new SarifRoot(
            Version: "2.1.0",
            Schema: "https://json.schemastore.org/sarif-2.1.0.json",
            Runs: new[]
            {
                new
                {
                    tool = new
                    {
                        driver = new
                        {
                            name = "dotnet-diff-coverage",
                            version = ToolVersion,
                            informationUri = "https://github.com/mr-miles/dotnet-diff-coverage",
                            rules = new[]
                            {
                                new
                                {
                                    id = "DC001",
                                    name = "UncoveredAddedLine",
                                    shortDescription = new { text = "Added line not covered by tests" },
                                    fullDescription = new { text = "A line was added in this diff but is not covered by any test in the provided coverage reports." },
                                    helpUri = "https://github.com/mr-miles/dotnet-diff-coverage#dc001",
                                    defaultConfiguration = new { level = "warning" },
                                },
                            },
                        },
                    },
                    results,
                },
            }
        );

        await JsonSerializer.SerializeAsync(stream, sarif, Options);
    }
}
