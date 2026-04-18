using DotnetDiffCoverage.Analysis;
using DotnetDiffCoverage.Config;
using DotnetDiffCoverage.Output;
using DotnetDiffCoverage.Parsing;
using DotnetDiffCoverage.Parsing.Formats;
using Microsoft.Extensions.DependencyInjection;

namespace DotnetDiffCoverage.Services;

public static class ServiceRegistration
{
    public static IServiceCollection AddDiffCoverageServices(this IServiceCollection services)
    {
        // Diff parsing
        services.AddTransient<DiffParser>();

        // Coverage parsing
        services.AddTransient<ICoverageFormatParser, CoberturaCoverageParser>();
        services.AddTransient<ICoverageFormatParser, OpenCoverCoverageParser>();
        services.AddTransient<ICoverageFormatParser, LcovCoverageParser>();
        services.AddTransient<CoverageParser>();

        // Cross-reference engine
        services.AddTransient<CrossReferenceEngine>();

        // Config
        services.AddTransient<ConfigLoader>();

        // Output formatters
        services.AddTransient<JsonReporter>();
        services.AddTransient<SarifReporter>();

        return services;
    }
}
