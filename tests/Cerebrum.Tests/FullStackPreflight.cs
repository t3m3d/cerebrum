using System.Text.Json;
using Cerebrum.Core.Components;
using Cerebrum.Core.Configuration;
using Cerebrum.Core.Discovery;

namespace Cerebrum.Tests;

internal static class FullStackPreflight
{
    private const ushort Amd64Machine = 0x8664;
    private const uint PortableExecutableSignature = 0x00004550;

    internal static int Run(IReadOnlyList<string> arguments)
    {
        if (arguments.Count > 1)
        {
            Console.Error.WriteLine("Usage: Cerebrum.Tests --full-stack-preflight [Debug|Release]");
            return 2;
        }

        var configuration = arguments.Count == 0 ? "Debug" : arguments[0];
        if (configuration is not ("Debug" or "Release"))
        {
            Console.Error.WriteLine("The preflight configuration must be Debug or Release.");
            return 2;
        }

        var repositoryRoot = RepositoryLocator.FindRepositoryRoot(AppContext.BaseDirectory);
        if (repositoryRoot is null)
        {
            Console.Error.WriteLine("FAIL Cerebrum repository root was not found.");
            return 1;
        }

        var resolver = new ComponentExecutableResolver(repositoryRoot, configuration);
        var settings = new CerebrumSettings();
        var failures = 0;

        foreach (var definition in ComponentCatalog.All)
        {
            var resolved = resolver.Resolve(definition, settings);
            if (resolved is null)
            {
                if (definition.Activation == ComponentActivation.ExternalSupervisor)
                {
                    Console.WriteLine($"OPTIONAL {definition.Id}: external supervisor build not found.");
                    continue;
                }

                failures++;
                Console.Error.WriteLine(
                    $"MISSING {definition.Id}: build {definition.RepositoryDirectory}/{definition.ProjectDirectory}.");
                continue;
            }

            try
            {
                var targetFramework = ValidateComponent(repositoryRoot, resolved);
                Console.WriteLine(
                    $"PASS {definition.Id}: source={resolved.Source}, machine=x64, tfm={targetFramework}");
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException
                    or InvalidDataException or JsonException)
            {
                failures++;
                Console.Error.WriteLine($"FAIL {definition.Id}: {exception.Message}");
            }
        }

        Console.WriteLine(
            failures == 0
                ? $"FULL_STACK_PREFLIGHT_OK components={ComponentCatalog.All.Count} launched=0"
                : $"FULL_STACK_PREFLIGHT_FAILED failures={failures}");
        return failures == 0 ? 0 : 1;
    }

    private static string ValidateComponent(
        string cerebrumRepositoryRoot,
        ResolvedComponent resolved)
    {
        var definition = resolved.Definition;
        var executablePath = Path.GetFullPath(resolved.Path);
        if (!File.Exists(executablePath))
        {
            throw new InvalidDataException("The resolved executable no longer exists.");
        }

        if (!string.Equals(
                Path.GetFileName(executablePath),
                definition.ExecutableName,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The resolved executable name does not match the catalog.");
        }

        if (ReadMachine(executablePath) != Amd64Machine)
        {
            throw new InvalidDataException("The resolved executable is not an x64 PE image.");
        }

        if (definition.Id == ComponentId.Wallpaper)
        {
            return "native";
        }

        var runtimeConfigPath = Path.ChangeExtension(executablePath, ".runtimeconfig.json");
        var dependenciesPath = Path.ChangeExtension(executablePath, ".deps.json");
        if (!File.Exists(runtimeConfigPath) || !File.Exists(dependenciesPath))
        {
            throw new InvalidDataException("The executable is missing its .NET runtime or dependency manifest.");
        }

        if (string.Equals(resolved.Source, "sibling repository build", StringComparison.Ordinal))
        {
            var expectedRepositoryRoot = definition.IsInternal
                ? cerebrumRepositoryRoot
                : Path.Combine(
                    Directory.GetParent(cerebrumRepositoryRoot)?.FullName ?? cerebrumRepositoryRoot,
                    definition.RepositoryDirectory);
            if (!IsWithin(expectedRepositoryRoot, executablePath))
            {
                throw new InvalidDataException("Sibling discovery escaped the component repository.");
            }
        }

        using var document = JsonDocument.Parse(File.ReadAllBytes(runtimeConfigPath));
        if (!document.RootElement.TryGetProperty("runtimeOptions", out var runtimeOptions)
            || !runtimeOptions.TryGetProperty("tfm", out var targetFrameworkElement))
        {
            throw new InvalidDataException("The runtime manifest does not declare a target framework.");
        }

        var targetFramework = targetFrameworkElement.GetString();
        if (string.IsNullOrWhiteSpace(targetFramework)
            || !targetFramework.StartsWith("net8.0", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The component does not target the supported .NET 8 runtime.");
        }

        return targetFramework;
    }

    private static ushort ReadMachine(string executablePath)
    {
        using var stream = new FileStream(
            executablePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var reader = new BinaryReader(stream);
        if (stream.Length < 64 || reader.ReadUInt16() != 0x5A4D)
        {
            throw new InvalidDataException("The executable does not contain a valid DOS header.");
        }

        stream.Position = 0x3C;
        var headerOffset = reader.ReadInt32();
        if (headerOffset < 64 || headerOffset > stream.Length - 6)
        {
            throw new InvalidDataException("The executable contains an invalid PE header offset.");
        }

        stream.Position = headerOffset;
        if (reader.ReadUInt32() != PortableExecutableSignature)
        {
            throw new InvalidDataException("The executable does not contain a valid PE signature.");
        }

        return reader.ReadUInt16();
    }

    private static bool IsWithin(string rootPath, string candidatePath)
    {
        var relative = Path.GetRelativePath(Path.GetFullPath(rootPath), candidatePath);
        return !Path.IsPathRooted(relative)
            && !string.Equals(relative, "..", StringComparison.Ordinal)
            && !relative.StartsWith(
                $"..{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal);
    }
}
