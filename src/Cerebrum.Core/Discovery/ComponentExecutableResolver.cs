using Cerebrum.Core.Components;
using Cerebrum.Core.Configuration;

namespace Cerebrum.Core.Discovery;

public sealed class ComponentExecutableResolver
{
    private readonly string? _cerebrumRepositoryRoot;
    private readonly string _preferredConfiguration;

    public ComponentExecutableResolver(string? cerebrumRepositoryRoot, string preferredConfiguration)
    {
        _cerebrumRepositoryRoot = cerebrumRepositoryRoot;
        _preferredConfiguration = preferredConfiguration;
    }

    public ResolvedComponent? Resolve(ComponentDefinition definition, CerebrumSettings settings)
    {
        var explicitPath = GetConfiguredPath(definition.Id, settings.Components);
        if (IsExecutable(explicitPath))
        {
            return new(definition, Path.GetFullPath(explicitPath!), "settings");
        }

        var environmentPath = Environment.GetEnvironmentVariable(definition.EnvironmentVariable);
        if (IsExecutable(environmentPath))
        {
            return new(definition, Path.GetFullPath(environmentPath!), "environment");
        }

        var installedPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Cerebrum",
            definition.DisplayName,
            "current",
            definition.ExecutableName);
        if (File.Exists(installedPath))
        {
            return new(definition, installedPath, "installed");
        }

        var developmentPath = FindDevelopmentBuild(definition);
        if (developmentPath is not null)
        {
            return new(definition, developmentPath, "sibling repository build");
        }

        var pathEntry = FindOnPath(definition.ExecutableName);
        return pathEntry is null ? null : new(definition, pathEntry, "PATH");
    }

    private string? FindDevelopmentBuild(ComponentDefinition definition)
    {
        if (_cerebrumRepositoryRoot is null)
        {
            return null;
        }

        var repositoryRoot = definition.IsInternal
            ? _cerebrumRepositoryRoot
            : Path.Combine(
                Directory.GetParent(_cerebrumRepositoryRoot)?.FullName ?? _cerebrumRepositoryRoot,
                definition.RepositoryDirectory);
        var sourceRoot = Path.Combine(repositoryRoot, "src");
        if (!Directory.Exists(sourceRoot))
        {
            return null;
        }

        try
        {
            var candidates = Directory.EnumerateFiles(
                    sourceRoot,
                    definition.ExecutableName,
                    SearchOption.AllDirectories)
                .Where(path => path.Contains(
                    $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                    StringComparison.OrdinalIgnoreCase))
                .Where(path => !path.Contains(
                    $"{Path.DirectorySeparatorChar}publish{Path.DirectorySeparatorChar}",
                    StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => ConfigurationRank(path))
                .ThenByDescending(File.GetLastWriteTimeUtc)
                .ToArray();
            return candidates.FirstOrDefault();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private int ConfigurationRank(string path)
    {
        if (path.Contains(
            $"{Path.DirectorySeparatorChar}{_preferredConfiguration}{Path.DirectorySeparatorChar}",
            StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        return path.Contains(
            $"{Path.DirectorySeparatorChar}Release{Path.DirectorySeparatorChar}",
            StringComparison.OrdinalIgnoreCase)
            ? 1
            : 2;
    }

    private static string? FindOnPath(string executableName)
    {
        var pathValue = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathValue))
        {
            return null;
        }

        foreach (var entry in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(entry.Trim(), executableName);
                if (File.Exists(candidate))
                {
                    return Path.GetFullPath(candidate);
                }
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
            {
                // Ignore malformed PATH entries and continue with the remaining entries.
            }
        }

        return null;
    }

    private static string? GetConfiguredPath(ComponentId id, ComponentPaths paths) => id switch
    {
        ComponentId.Broker => paths.Broker,
        ComponentId.Medulla => paths.Medulla,
        ComponentId.Thalamus => paths.Thalamus,
        ComponentId.Cortex => paths.Cortex,
        _ => null
    };

    private static bool IsExecutable(string? path) =>
        !string.IsNullOrWhiteSpace(path)
        && Path.IsPathFullyQualified(path)
        && File.Exists(path);
}
