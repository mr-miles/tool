namespace DotnetDiffCoverage.Config;

/// <summary>
/// Represents the configuration for dotnet-diff-coverage, loadable from a JSON config file.
/// CLI arguments take precedence over config file values.
/// </summary>
public sealed class ToolConfig
{
    /// <summary>Glob patterns for files to treat as test code (excluded from uncovered analysis).</summary>
    public IReadOnlyList<string> TestFilePatterns { get; init; } = DefaultTestFilePatterns;

    /// <summary>Glob patterns for non-source files to exclude from diff analysis entirely.</summary>
    public IReadOnlyList<string> ExcludePatterns { get; init; } = DefaultExcludePatterns;

    /// <summary>Coverage threshold (0â100). Exit code 1 if uncovered % exceeds this.</summary>
    public double Threshold { get; init; } = 0.0;

    /// <summary>Default coverage format if not specified on CLI.</summary>
    public string? CoverageFormat { get; init; }

    /// <summary>Prefix to strip from coverage file paths before matching against diff paths.</summary>
    public string? CoveragePathPrefix { get; init; }

    public static readonly IReadOnlyList<string> DefaultTestFilePatterns = new[]
    {
        "tests/**",
        "test/**",
        "**/*.Tests.cs",
        "**/*.Test.cs",
        "**/*.Spec.cs",
        "**/*Tests.cs",
        "**/*Test.cs",
        "**/*Specs.cs",
        "**/*Fixture.cs",
        "**/*Fixtures.cs",
    };

    public static readonly IReadOnlyList<string> DefaultExcludePatterns = new[]
    {
        "**/*.yml",
        "**/*.yaml",
        "**/*.json",
        "**/*.md",
        "**/*.xml",
        "**/*.txt",
        "**/*.csproj",
        "**/*.sln",
        "**/*.props",
        "**/*.targets",
        "**/*.config",
    };
}
