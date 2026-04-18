using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace DotnetDiffCoverage.Config;

public sealed class ConfigLoader(ILogger<ConfigLoader> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private const string DefaultConfigFileName = "dotnet-diff-coverage.json";

    /// <summary>
    /// Loads config from the specified file, or searches for dotnet-diff-coverage.json
    /// in the current directory and its parents. Returns a default config if not found.
    /// </summary>
    public ToolConfig Load(FileInfo? configFile)
    {
        var file = configFile ?? FindDefaultConfig();
        if (file is null)
        {
            logger.LogDebug("No config file found; using defaults");
            return new ToolConfig();
        }

        if (!file.Exists)
        {
            logger.LogWarning("Config file '{Path}' not found; using defaults", file.FullName);
            return new ToolConfig();
        }

        try
        {
            using var stream = file.OpenRead();
            var config = JsonSerializer.Deserialize<ToolConfig>(stream, JsonOptions);
            if (config is null)
            {
                logger.LogWarning("Config file '{Path}' deserialized to null; using defaults", file.FullName);
                return new ToolConfig();
            }
            logger.LogDebug("Loaded config from '{Path}'", file.FullName);
            return config;
        }
        catch (JsonException ex)
        {
            logger.LogWarning("Failed to parse config file '{Path}': {Message}; using defaults",
                file.FullName, ex.Message);
            return new ToolConfig();
        }
    }

    private static FileInfo? FindDefaultConfig()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            var candidate = new FileInfo(Path.Combine(dir.FullName, DefaultConfigFileName));
            if (candidate.Exists) return candidate;
            dir = dir.Parent;
        }
        return null;
    }
}
