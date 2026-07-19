using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cerebrum.Core.Performance;

namespace Cerebrum.Tests;

internal static class PerformanceCapture
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public static async Task<int> RunCaptureAsync(string[] args)
    {
        if (args.Length != 3 ||
            !Enum.TryParse<DesktopPerformanceProfile>(args[0], ignoreCase: true, out var profile) ||
            !int.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) ||
            seconds is < 1 or > 300)
        {
            Console.Error.WriteLine(
                "Usage: --performance-capture <stock|compatibility|lite> <seconds:1-300> <output.json>");
            return 2;
        }

        string outputPath;
        try
        {
            outputPath = Path.GetFullPath(args[2]);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            IOException or
            NotSupportedException or
            UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Invalid performance output path: {exception.Message}");
            return 2;
        }

        var collector = new DesktopResourceCollector();
        var snapshot = await collector.CaptureAsync(
            profile,
            TimeSpan.FromSeconds(seconds)).ConfigureAwait(false);
        await File.WriteAllTextAsync(
            outputPath,
            JsonSerializer.Serialize(snapshot, JsonOptions)).ConfigureAwait(false);

        Console.WriteLine(
            $"PERF_CAPTURE profile={profile.ToString().ToLowerInvariant()} " +
            $"durationSeconds={snapshot.SampleDurationSeconds:F3} " +
            $"desktopProcesses={snapshot.DesktopTotals.ProcessCount} " +
            $"privateMiB={ToMebibytes(snapshot.DesktopTotals.PrivateBytes):F1} " +
            $"workingSetMiB={ToMebibytes(snapshot.DesktopTotals.WorkingSetBytes):F1} " +
            $"cpuPercent={snapshot.DesktopTotals.CpuPercent:F4} " +
            $"handles={snapshot.DesktopTotals.HandleCount}");

        var failures = DesktopPerformancePolicy.ValidateProfile(snapshot);
        foreach (var failure in failures)
        {
            Console.Error.WriteLine($"PERF_PROFILE_INVALID {failure}");
        }

        Console.WriteLine(
            $"{(failures.Count == 0 ? "PERF_CAPTURE_OK" : "PERF_CAPTURE_INVALID")} path={outputPath}");
        return failures.Count == 0 ? 0 : 1;
    }

    public static async Task<int> RunCompareAsync(string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine(
                "Usage: --performance-compare <stock-baseline.json> <candidate.json>");
            return 2;
        }

        DesktopResourceSnapshot baseline;
        DesktopResourceSnapshot candidate;
        try
        {
            baseline = await ReadSnapshotAsync(args[0]).ConfigureAwait(false);
            candidate = await ReadSnapshotAsync(args[1]).ConfigureAwait(false);
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            IOException or
            JsonException or
            NotSupportedException or
            UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Could not read performance snapshot: {exception.Message}");
            return 2;
        }

        var comparison = DesktopPerformancePolicy.Compare(baseline, candidate);
        Console.WriteLine(
            $"PERF_COMPARE baseline={baseline.Profile.ToString().ToLowerInvariant()} " +
            $"candidate={candidate.Profile.ToString().ToLowerInvariant()} " +
            $"privateReductionPercent={Format(comparison.PrivateBytesReductionPercent)} " +
            $"workingSetReductionPercent={Format(comparison.WorkingSetReductionPercent)} " +
            $"cpuChangePercentagePoints={comparison.CpuChangePercentagePoints:F4} " +
            $"handleChangePercent={Format(comparison.HandleCountChangePercent)}");
        foreach (var failure in comparison.Failures)
        {
            Console.Error.WriteLine($"PERF_BUDGET_FAILED {failure}");
        }

        Console.WriteLine(
            comparison.MeetsLighterTarget
                ? "PERF_COMPARE_OK candidate meets the lighter-desktop budget"
                : "PERF_COMPARE_FAILED candidate does not meet the lighter-desktop budget");
        return comparison.MeetsLighterTarget ? 0 : 1;
    }

    private static async Task<DesktopResourceSnapshot> ReadSnapshotAsync(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var json = await File.ReadAllTextAsync(fullPath).ConfigureAwait(false);
        return JsonSerializer.Deserialize<DesktopResourceSnapshot>(json, JsonOptions)
            ?? throw new JsonException("The snapshot document was empty.");
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }

    private static double ToMebibytes(long bytes) => bytes / (1024d * 1024d);

    private static string Format(double? value) =>
        value?.ToString("F2", CultureInfo.InvariantCulture) ?? "n/a";
}
