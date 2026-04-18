using System.CommandLine;
using DotnetDiffCoverage.Analysis;
using DotnetDiffCoverage.Config;
using DotnetDiffCoverage.Output;
using DotnetDiffCoverage.Parsing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using DotnetDiffCoverage.Services;

namespace DotnetDiffCoverage.Commands;

public static class RootCommandBuilder
{
    /// <summary>Builds the root command using a default DI host. Useful for testing.</summary>
    public static RootCommand Build()
    {
        var host = Host.CreateDefaultBuilder()
            .ConfigureServices((_, services) => services.AddDiffCoverageServices())
            .Build();
        return Build(host);
    }

    public static RootCommand Build(IHost host)
    {
        var rootCommand = new RootCommand(
            "Cross-references a code diff with .NET coverage files to surface uncovered lines introduced by a PR.");

        // --diff: path to a unified diff file, or '-' for stdin
        var diffOption = new Option<FileInfo?>(
            name: "--diff",
            description: "Path to a unified diff (.patch) file. Use '-' to read from stdin.")
        {
            IsRequired = false,
        };

        // --coverage: one or more coverage file paths (Cobertura, OpenCover, or LCOV)
        var coverageOption = new Option<FileInfo[]>(
            name: "--coverage",
            description: "One or more coverage report files (Cobertura XML, OpenCover XML, or LCOV).")
        {
            AllowMultipleArgumentsPerToken = false,
            IsRequired = false,
        };
        coverageOption.Arity = ArgumentArity.OneOrMore;

        // --coverage-format: specifies the coverage file format (cobertura, opencover, lcov)
        var coverageFormatOption = new Option<string?>(
            name: "--coverage-format",
            description: "Coverage file format: cobertura, opencover, or lcov. Required when --coverage is provided.");

        // --output-json: write JSON report to this path (use '-' for stdout)
        var outputJsonOption = new Option<FileInfo?>(
            name: "--output-json",
            description: "Write JSON coverage-diff report to this file path. Use '-' for stdout.");

        // --output-sarif: write SARIF 2.1.0 report to this path
        var outputSarifOption = new Option<FileInfo?>(
            name: "--output-sarif",
            description: "Write SARIF 2.1.0 report to this file path for use with GitHub/ADO annotations.");

        // --threshold: maximum allowed uncovered-line percentage (0-100, default 0)
        var thresholdOption = new Option<double>(
            name: "--threshold",
            getDefaultValue: () => 0.0,
            description: "Maximum allowed percentage of uncovered diff lines before exit code 1 (0-100, default 0).");

        // --config: path to dotnet-diff-coverage.json config file
        var configOption = new Option<FileInfo?>(
            name: "--config",
            description: "Path to a JSON config file. Defaults to dotnet-diff-coverage.json in the current directory.");

        // --coverage-path-prefix: prefix to strip from coverage file paths so they match diff paths exactly
        var coveragePathPrefixOption = new Option<string?>(
            name: "--coverage-path-prefix",
            description: "Prefix to strip from coverage file paths before matching against diff paths. " +
                         "Use when coverage paths are absolute (e.g. /home/ci/repo/) and diff paths are relative (e.g. src/Foo.cs).");

        // --no-color: suppress ANSI color codes in console output
        var noColorOption = new Option<bool>(
            name: "--no-color",
            description: "Suppress ANSI color codes in console output.");

        rootCommand.AddOption(diffOption);
        rootCommand.AddOption(coverageOption);
        rootCommand.AddOption(coverageFormatOption);
        rootCommand.AddOption(coveragePathPrefixOption);
        rootCommand.AddOption(outputJsonOption);
        rootCommand.AddOption(outputSarifOption);
        rootCommand.AddOption(thresholdOption);
        rootCommand.AddOption(configOption);
        rootCommand.AddOption(noColorOption);

        rootCommand.SetHandler(async (context) =>
        {
            var diffFile = context.ParseResult.GetValueForOption(diffOption);
            var coverageFiles = context.ParseResult.GetValueForOption(coverageOption);
            var coverageFormat = context.ParseResult.GetValueForOption(coverageFormatOption);
            var coveragePathPrefix = context.ParseResult.GetValueForOption(coveragePathPrefixOption);
            var outputJson = context.ParseResult.GetValueForOption(outputJsonOption);
            var outputSarif = context.ParseResult.GetValueForOption(outputSarifOption);
            var threshold = context.ParseResult.GetValueForOption(thresholdOption);
            var configFile = context.ParseResult.GetValueForOption(configOption);
            var noColor = context.ParseResult.GetValueForOption(noColorOption);

            context.ExitCode = await HandleAsync(
                diffFile, coverageFiles, coverageFormat, coveragePathPrefix,
                outputJson, outputSarif, threshold, configFile, noColor, host);
        });

        return rootCommand;
    }

    private static async Task<int> HandleAsync(
        FileInfo? diffFile,
        FileInfo[]? coverageFiles,
        string? coverageFormat,
        string? coveragePathPrefix,
        FileInfo? outputJson,
        FileInfo? outputSarif,
        double threshold,
        FileInfo? configFile,
        bool noColor,
        IHost host)
    {
        var services = host.Services;
        var configLoader = services.GetRequiredService<ConfigLoader>();
        var diffParser = services.GetRequiredService<DiffParser>();
        var coverageParser = services.GetRequiredService<CoverageParser>();
        var engine = services.GetRequiredService<CrossReferenceEngine>();

        // Load config (CLI args override config file values)
        var config = configLoader.Load(configFile);
        var effectiveThreshold = threshold > 0 ? threshold : config.Threshold;
        var effectiveFormat = coverageFormat ?? config.CoverageFormat;
        var effectivePrefix = coveragePathPrefix ?? config.CoveragePathPrefix;

        // Parse diff
        DiffResult diff;
        if (diffFile is not null)
        {
            string content;
            if (diffFile.Name == "-")
            {
                using var reader = new StreamReader(Console.OpenStandardInput());
                content = await reader.ReadToEndAsync();
            }
            else
            {
                if (!diffFile.Exists)
                {
                    Console.Error.WriteLine($"Diff file not found: {diffFile.FullName}");
                    return 2;
                }
                content = await File.ReadAllTextAsync(diffFile.FullName);
            }
            diff = diffParser.Parse(content);
        }
        else
        {
            // No diff provided: print usage hint and exit cleanly.
            // (Equivalent to running with --help for the common "no args" case.)
            Console.WriteLine("Usage: dotnet-diff-coverage --diff <file|-> --coverage <file> --format <format>");
            Console.WriteLine("Run with --help for full usage information.");
            return 0;
        }

        // Filter test files from diff
        var filteredDiff = TestFileFilter.ExcludeTestFiles(diff, config.TestFilePatterns);

        // Parse coverage
        var coverage = CoverageResult.Empty;
        if (coverageFiles is { Length: > 0 })
        {
            foreach (var file in coverageFiles)
            {
                if (!file.Exists)
                {
                    Console.Error.WriteLine($"Coverage file not found: {file.FullName}");
                    return 2;
                }
            }

            if (effectiveFormat is null)
            {
                Console.Error.WriteLine("Coverage format required. Use --coverage-format cobertura|opencover|lcov.");
                return 2;
            }

            if (!Enum.TryParse<CoverageFormat>(effectiveFormat, ignoreCase: true, out var parsedFormat)
                || parsedFormat == CoverageFormat.Unknown)
            {
                Console.Error.WriteLine($"Unknown coverage format: '{effectiveFormat}'. Valid values: cobertura, opencover, lcov.");
                return 2;
            }

            // Merge coverage from all supplied files
            var mergedLines = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in coverageFiles)
            {
                var partial = coverageParser.Parse(file.FullName, parsedFormat);
                foreach (var (path, lines) in partial.FileCoveredLines)
                {
                    if (!mergedLines.TryGetValue(path, out var existing))
                    {
                        existing = new HashSet<int>();
                        mergedLines[path] = existing;
                    }
                    foreach (var line in lines)
                        existing.Add(line);
                }
            }

            var readOnlyMerged = mergedLines.ToDictionary(
                kvp => kvp.Key,
                kvp => (IReadOnlySet<int>)kvp.Value,
                StringComparer.OrdinalIgnoreCase);
            coverage = new CoverageResult(readOnlyMerged);
        }

        // Cross-reference diff against coverage
        var result = engine.Analyze(filteredDiff, coverage, effectivePrefix);

        // Console output
        var consoleReporter = new ConsoleReporter(noColor);
        consoleReporter.Write(result);

        // JSON output
        if (outputJson is not null)
        {
            var jsonReporter = services.GetRequiredService<JsonReporter>();
            if (outputJson.Name == "-")
            {
                await jsonReporter.WriteAsync(result, Console.OpenStandardOutput());
            }
            else
            {
                using var stream = outputJson.Open(FileMode.Create);
                await jsonReporter.WriteAsync(result, stream);
            }
        }

        // SARIF output
        if (outputSarif is not null)
        {
            var sarifReporter = services.GetRequiredService<SarifReporter>();
            if (outputSarif.Name == "-")
            {
                await sarifReporter.WriteAsync(result, Console.OpenStandardOutput());
            }
            else
            {
                using var stream = outputSarif.Open(FileMode.Create);
                await sarifReporter.WriteAsync(result, stream);
            }
        }

        // Threshold check
        if (effectiveThreshold > 0 && result.UncoveredPercent > effectiveThreshold)
        {
            Console.Error.WriteLine(
                $"Coverage threshold exceeded: {result.UncoveredPercent:F1}% uncovered > {effectiveThreshold}% limit.");
            return 1;
        }

        return 0;
    }
}
