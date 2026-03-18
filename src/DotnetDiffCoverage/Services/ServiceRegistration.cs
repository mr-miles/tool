using DotnetDiffCoverage.Parsing;
using Microsoft.Extensions.DependencyInjection;

namespace DotnetDiffCoverage.Services;

public static class ServiceRegistration
{
    public static IServiceCollection AddDiffCoverageServices(this IServiceCollection services)
    {
        // Diff parsing
        services.AddTransient<DiffParser>();

        // Coverage parsers, formatters, and API clients will be registered here in later phases.
        return services;
    }
}
