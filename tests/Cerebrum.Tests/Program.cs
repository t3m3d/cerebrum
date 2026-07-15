using System.Text.Json;
using Cerebrum.Core.Components;
using Cerebrum.Core.Configuration;
using Cerebrum.Core.Discovery;
using Cerebrum.Core.Protocol;

namespace Cerebrum.Tests;

internal static class Program
{
    public static async Task<int> Main()
    {
        var tests = new (string Name, Func<Task> Run)[]
        {
            ("component catalog ownership", TestComponentCatalogAsync),
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
        Assert(ComponentCatalog.Get(ComponentId.Medulla).StartsWithSession, "Medulla must start with the session.");
        Assert(ComponentCatalog.Get(ComponentId.Thalamus).StartsWithSession, "Thalamus must start with the session.");
        Assert(!ComponentCatalog.Get(ComponentId.Cortex).StartsWithSession, "Cortex must remain on demand.");
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
