using System.Text.Json;
using Cerebrum.Core.Components;
using Cerebrum.Core.Configuration;
using Cerebrum.Core.Discovery;
using Cerebrum.Core.Performance;
using Cerebrum.Core.Protocol;

namespace Cerebrum.Tests;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length > 0)
        {
            if (string.Equals(args[0], "--full-stack-preflight", StringComparison.Ordinal))
                return FullStackPreflight.Run(args[1..]);

            if (string.Equals(args[0], "--performance-capture", StringComparison.Ordinal))
                return await PerformanceCapture.RunCaptureAsync(args[1..]).ConfigureAwait(false);

            if (string.Equals(args[0], "--performance-compare", StringComparison.Ordinal))
                return await PerformanceCapture.RunCompareAsync(args[1..]).ConfigureAwait(false);

            Console.Error.WriteLine("Unknown mode. Use --full-stack-preflight, --performance-capture, or --performance-compare.");
            return 2;
        }

        var tests = new (string Name, Func<Task> Run)[]
        {
            ("component catalog ownership", TestComponentCatalogAsync),
            ("component command contracts", TestComponentCommandsAsync),
            ("desktop performance policy", TestDesktopPerformancePolicyAsync),
            ("settings semantic policy", TestSettingsPolicyAsync),
            ("atomic settings and backup recovery", TestAtomicSettingsAsync),
            ("absolute data-root policy", TestDataRootAsync),
            ("configured executable discovery", TestExecutableResolverAsync),
            ("repository root discovery", TestRepositoryLocatorAsync),
            ("broker protocol round trip", TestBrokerProtocolAsync)
        };

        var failures = 0;
        foreach (var test in tests)
        {
            try
            {
                await test.Run().ConfigureAwait(false);
                Console.WriteLine($"PASS {test.Name}");
            }
            catch (Exception exception)
            {
                failures++;
                Console.Error.WriteLine($"FAIL {test.Name}: {exception.Message}");
            }
        }

        Console.WriteLine($"{tests.Length - failures}/{tests.Length} tests passed");
        return failures == 0 ? 0 : 1;
    }

    private static Task TestComponentCatalogAsync()
    {
        Assert(ComponentCatalog.All.Count == 4, "The catalog must contain four owned boundaries.");
        Assert(ComponentCatalog.Get(ComponentId.Broker).IsInternal, "The broker belongs to the Cerebrum repository.");
        Assert(!ComponentCatalog.Get(ComponentId.Broker).StartsWithSession, "The version-one broker must remain on demand.");
        Assert(ComponentCatalog.Get(ComponentId.Medulla).StartsWithSession, "Medulla must start with the session.");
        Assert(ComponentCatalog.Get(ComponentId.Thalamus).StartsWithSession, "Thalamus must start with the session.");
        Assert(!ComponentCatalog.Get(ComponentId.Cortex).StartsWithSession, "Cortex must remain on demand.");
        Assert(
            ComponentCatalog.Get(ComponentId.Cortex).RepositoryDirectory == "cortex-win",
            "Cerebrum must discover the Windows Cortex repository.");
        return Task.CompletedTask;
    }

    private static Task TestComponentCommandsAsync()
    {
        var path = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "Cerebrum Cortex Contract"));
        var cortex = ComponentCommands.CortexOpen(path);
        Assert(
            cortex.SequenceEqual(["--open", path]),
            "Cortex must receive an explicit open action and one fully qualified path argument.");
        Assert(
            ComponentCommands.ThalamusOverview().SequenceEqual(["--overview"]),
            "Thalamus overview must use its documented command.");

        var rejectedRelativePath = false;
        try
        {
            _ = ComponentCommands.CortexOpen("relative-path");
        }
        catch (ArgumentException)
        {
            rejectedRelativePath = true;
        }

        Assert(rejectedRelativePath, "Cortex integration must reject relative paths.");
        return Task.CompletedTask;
    }

    private static Task TestDesktopPerformancePolicyAsync()
    {
        Assert(
            DesktopProcessCatalog.TryGetGroup("explorer", out var explorerGroup) &&
            explorerGroup == DesktopProcessGroup.WindowsShell,
            "Explorer must be counted in the Windows shell group.");
        Assert(
            DesktopProcessCatalog.TryGetGroup("Cortex", out var cortexGroup) &&
            cortexGroup == DesktopProcessGroup.OnDemand,
            "Cortex must be counted as on demand.");
        Assert(
            DesktopProcessCatalog.TryGetGroup("Cerebrum.Host", out var hostGroup) &&
            hostGroup == DesktopProcessGroup.CerebrumSession,
            "The host must be counted in the Cerebrum session.");
        Assert(
            DesktopProcessCatalog.TryGetGroup("dwm", out var compositorGroup) &&
            compositorGroup == DesktopProcessGroup.Compositor,
            "DWM must be reported separately from the desktop budget.");
        Assert(
            !DesktopProcessCatalog.TryGetGroup("notepad", out _),
            "Unrelated applications must not affect the desktop budget.");

        const long mebibyte = 1024L * 1024L;
        var capturedAt = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var stock = DesktopResourceSnapshot.Create(
            DesktopPerformanceProfile.Stock,
            capturedAt,
            TimeSpan.FromSeconds(15),
            8,
            [
                new(
                    1,
                    "explorer",
                    DesktopProcessGroup.WindowsShell,
                    500 * mebibyte,
                    450 * mebibyte,
                    100,
                    40,
                    0.20,
                    CpuSampled: true)
            ]);
        var lite = DesktopResourceSnapshot.Create(
            DesktopPerformanceProfile.Lite,
            capturedAt,
            TimeSpan.FromSeconds(15),
            8,
            [
                new(2, "Cerebrum.Host", DesktopProcessGroup.CerebrumSession, 150 * mebibyte, 150 * mebibyte, 35, 12, 0.10, true),
                new(3, "Medulla", DesktopProcessGroup.CerebrumSession, 130 * mebibyte, 140 * mebibyte, 35, 12, 0.10, true),
                new(4, "Thalamus", DesktopProcessGroup.CerebrumSession, 120 * mebibyte, 130 * mebibyte, 35, 12, 0.10, true)
            ]);

        Assert(DesktopPerformancePolicy.ValidateProfile(stock).Count == 0, "The synthetic stock profile must be valid.");
        Assert(DesktopPerformancePolicy.ValidateProfile(lite).Count == 0, "The synthetic Lite profile must be valid.");
        var passing = DesktopPerformancePolicy.Compare(stock, lite);
        Assert(passing.MeetsLighterTarget, "A complete Lite profile with a 20% private-byte reduction must pass.");
        Assert(passing.PrivateBytesReductionPercent == 20d, "The private-byte reduction must be reported exactly.");

        var heavy = DesktopResourceSnapshot.Create(
            DesktopPerformanceProfile.Lite,
            capturedAt,
            TimeSpan.FromSeconds(15),
            8,
            [
                new(2, "Cerebrum.Host", DesktopProcessGroup.CerebrumSession, 180 * mebibyte, 170 * mebibyte, 35, 12, 0.10, true),
                new(3, "Medulla", DesktopProcessGroup.CerebrumSession, 160 * mebibyte, 160 * mebibyte, 35, 12, 0.10, true),
                new(4, "Thalamus", DesktopProcessGroup.CerebrumSession, 140 * mebibyte, 150 * mebibyte, 35, 12, 0.10, true)
            ]);
        var failing = DesktopPerformancePolicy.Compare(stock, heavy);
        Assert(!failing.MeetsLighterTarget, "A candidate with only a 4% private-byte reduction must fail.");
        Assert(
            failing.Failures.Any(failure => failure.Contains("10%", StringComparison.Ordinal)),
            "The failed comparison must identify the private-byte budget.");

        var incomplete = lite with
        {
            Processes = lite.Processes
                .Where(sample => !string.Equals(sample.ProcessName, "Thalamus", StringComparison.OrdinalIgnoreCase))
                .ToArray()
        };
        Assert(
            DesktopPerformancePolicy.ValidateProfile(incomplete).Any(
                failure => failure.Contains("Thalamus", StringComparison.Ordinal)),
            "An incomplete Lite session must not be accepted as lighter.");
        return Task.CompletedTask;
    }

    private static Task TestSettingsPolicyAsync()
    {
        Assert(CerebrumSettingsPolicy.IsValid(new()), "Defaults must be valid.");
        Assert(!CerebrumSettingsPolicy.IsValid(new() { Version = 99 }), "Unknown versions must be rejected.");
        Assert(!CerebrumSettingsPolicy.IsValid(new() { AccentColor = "violet" }), "Invalid colors must be rejected.");
        Assert(!CerebrumSettingsPolicy.IsValid(new() { WallpaperPath = "relative.png" }), "Relative wallpaper paths must be rejected.");
        return Task.CompletedTask;
    }

    private static async Task TestAtomicSettingsAsync()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var path = Path.Combine(root, "settings.json");
            var store = new AtomicSettingsStore<CerebrumSettings>(
                path,
                () => new(),
                CerebrumSettingsPolicy.IsValid);

            var first = new CerebrumSettings { ThemePreset = "Graphite" };
            var second = new CerebrumSettings { ThemePreset = "Frost" };
            await store.SaveAsync(first).ConfigureAwait(false);
            await store.SaveAsync(second).ConfigureAwait(false);
            var loaded = await store.LoadAsync().ConfigureAwait(false);
            Assert(loaded.ThemePreset == "Frost", "The current document must win.");

            await File.WriteAllTextAsync(path, "{broken").ConfigureAwait(false);
            var recovered = await store.LoadAsync().ConfigureAwait(false);
            Assert(recovered.ThemePreset == "Graphite", "A damaged current document must recover its last backup.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static Task TestDataRootAsync()
    {
        var absolute = Path.Combine(Path.GetTempPath(), "Cerebrum.Tests", Guid.NewGuid().ToString("N"));
        var accepted = DataRootResolver.Resolve(absolute);
        Assert(accepted.UsedOverride && !accepted.RejectedOverride, "An absolute override must be accepted.");

        var rejected = DataRootResolver.Resolve("relative-root");
        Assert(!rejected.UsedOverride && rejected.RejectedOverride, "A relative override must be rejected.");
        return Task.CompletedTask;
    }

    private static Task TestExecutableResolverAsync()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var executable = Path.Combine(root, "Medulla.exe");
            File.WriteAllBytes(executable, [0x4D, 0x5A]);
            var settings = new CerebrumSettings
            {
                Components = new() { Medulla = executable }
            };
            var resolver = new ComponentExecutableResolver(null, "Debug");
            var resolved = resolver.Resolve(ComponentCatalog.Get(ComponentId.Medulla), settings);
            Assert(resolved?.Path == executable, "The explicit component path must have first priority.");
            Assert(resolved?.Source == "settings", "The source must remain observable.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }

        var workspaceRoot = CreateTemporaryDirectory();
        try
        {
            var cerebrumRoot = Directory.CreateDirectory(
                Path.Combine(workspaceRoot, "cerebrum")).FullName;
            var resolver = new ComponentExecutableResolver(cerebrumRoot, "Debug");
            foreach (var catalogDefinition in ComponentCatalog.All)
            {
                var suffix = Guid.NewGuid().ToString("N");
                var definition = catalogDefinition with
                {
                    DisplayName = $"Preflight-{suffix}",
                    EnvironmentVariable = $"CEREBRUM_TEST_{suffix}"
                };
                var componentRoot = definition.IsInternal
                    ? cerebrumRoot
                    : Path.Combine(workspaceRoot, definition.RepositoryDirectory);
                var executable = Path.Combine(
                    componentRoot,
                    "src",
                    definition.ProjectDirectory,
                    "bin",
                    "x64",
                    "Debug",
                    "net8.0",
                    definition.ExecutableName);
                Directory.CreateDirectory(Path.GetDirectoryName(executable)!);
                File.WriteAllBytes(executable, [0x4D, 0x5A]);

                var resolved = resolver.Resolve(definition, new CerebrumSettings());
                Assert(resolved?.Path == executable, $"Sibling discovery failed for {definition.Id}.");
                Assert(resolved?.Source == "sibling repository build", "The discovery source must be explicit.");
            }
        }
        finally
        {
            Directory.Delete(workspaceRoot, recursive: true);
        }

        return Task.CompletedTask;
    }

    private static Task TestRepositoryLocatorAsync()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, ".git"));
            File.WriteAllText(Path.Combine(root, "README.md"), "test");
            var nested = Directory.CreateDirectory(Path.Combine(root, "src", "bin", "Debug"));
            Assert(RepositoryLocator.FindRepositoryRoot(nested.FullName) == root, "The nearest repository root must be found.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }

        return Task.CompletedTask;
    }

    private static Task TestBrokerProtocolAsync()
    {
        var request = new BrokerRequest(BrokerProtocol.Version, "request-1", BrokerProtocol.HealthCommand);
        var json = JsonSerializer.Serialize(request, BrokerProtocol.JsonOptions);
        var roundTrip = JsonSerializer.Deserialize<BrokerRequest>(json, BrokerProtocol.JsonOptions);
        Assert(roundTrip == request, "The broker request must round-trip exactly.");
        Assert(!BrokerProtocol.IsSupportedCommand("run-anything"), "The broker command list must be closed.");
        return Task.CompletedTask;
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "Cerebrum.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
