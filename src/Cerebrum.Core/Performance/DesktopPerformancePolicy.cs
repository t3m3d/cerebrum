namespace Cerebrum.Core.Performance;

public sealed record DesktopPerformanceComparison(
    bool MeetsLighterTarget,
    double? PrivateBytesReductionPercent,
    double? WorkingSetReductionPercent,
    double CpuChangePercentagePoints,
    double? HandleCountChangePercent,
    IReadOnlyList<string> Failures);

public static class DesktopPerformancePolicy
{
    public const double MaximumPrivateBytesRatio = 0.90;
    public const double MaximumCpuIncreasePercentagePoints = 0.25;
    public const double MaximumHandleCountRatio = 1.10;

    private static readonly string[] RequiredCerebrumProcesses =
    [
        "Cerebrum.Host",
        "krypton-wallpaper",
        "Parietal",
        "Medulla",
        "Thalamus"
    ];

    public static IReadOnlyList<string> ValidateProfile(DesktopResourceSnapshot snapshot)
    {
        var failures = new List<string>();
        if (snapshot.SchemaVersion != DesktopResourceSnapshot.CurrentSchemaVersion)
        {
            failures.Add($"Unsupported snapshot schema {snapshot.SchemaVersion}.");
        }

        if (snapshot.LogicalProcessorCount <= 0)
        {
            failures.Add("The logical-processor count must be greater than zero.");
        }

        if (!double.IsFinite(snapshot.SampleDurationSeconds) ||
            snapshot.SampleDurationSeconds < 1d)
        {
            failures.Add("The sample duration must be at least one second.");
        }

        if (snapshot.Processes.Select(sample => sample.ProcessId).Distinct().Count() !=
            snapshot.Processes.Count)
        {
            failures.Add("A performance snapshot cannot contain duplicate process IDs.");
        }

        foreach (var sample in snapshot.Processes)
        {
            if (sample.ProcessId <= 0 ||
                sample.PrivateBytes < 0 ||
                sample.WorkingSetBytes < 0 ||
                sample.HandleCount < 0 ||
                sample.ThreadCount < 0 ||
                !double.IsFinite(sample.CpuPercent) ||
                sample.CpuPercent < 0d)
            {
                failures.Add($"Process {sample.ProcessName} contains an invalid resource value.");
            }

            if (!DesktopProcessCatalog.TryGetGroup(sample.ProcessName, out var expectedGroup) ||
                sample.Group != expectedGroup)
            {
                failures.Add($"Process {sample.ProcessName} has an invalid desktop group.");
            }
        }

        var calculatedDesktopTotals = DesktopResourceTotals.Sum(
            snapshot.Processes.Where(sample => sample.Group != DesktopProcessGroup.Compositor));
        if (calculatedDesktopTotals != snapshot.DesktopTotals)
        {
            failures.Add("The stored desktop totals do not match the process samples.");
        }

        var calculatedCompositorTotals = DesktopResourceTotals.Sum(
            snapshot.Processes.Where(sample => sample.Group == DesktopProcessGroup.Compositor));
        if (calculatedCompositorTotals != snapshot.CompositorTotals)
        {
            failures.Add("The stored compositor totals do not match the process samples.");
        }

        var names = snapshot.Processes
            .Select(sample => sample.ProcessName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var hasExplorer = names.Contains("explorer");
        var hasCerebrum = snapshot.Processes.Any(
            sample => sample.Group == DesktopProcessGroup.CerebrumSession);

        switch (snapshot.Profile)
        {
            case DesktopPerformanceProfile.Stock:
                if (!hasExplorer)
                {
                    failures.Add("The stock profile requires Explorer.");
                }

                if (hasCerebrum)
                {
                    failures.Add("The stock profile cannot contain Cerebrum session processes.");
                }

                break;

            case DesktopPerformanceProfile.Compatibility:
                if (!hasExplorer)
                {
                    failures.Add("The compatibility profile requires Explorer.");
                }

                RequireCerebrumProcesses(names, failures);
                break;

            case DesktopPerformanceProfile.Lite:
                if (hasExplorer)
                {
                    failures.Add("The Lite profile cannot contain Explorer.");
                }

                RequireCerebrumProcesses(names, failures);
                break;

            default:
                failures.Add($"Unknown performance profile {snapshot.Profile}.");
                break;
        }

        if (snapshot.Processes.Any(sample => sample.Group == DesktopProcessGroup.OnDemand))
        {
            failures.Add("An idle profile cannot contain the on-demand Cortex process.");
        }

        if (snapshot.Processes.Any(
            sample => sample.Group != DesktopProcessGroup.Compositor && !sample.CpuSampled))
        {
            failures.Add("A tracked desktop process changed during CPU sampling; capture the profile again.");
        }

        return failures;
    }

    public static DesktopPerformanceComparison Compare(
        DesktopResourceSnapshot baseline,
        DesktopResourceSnapshot candidate)
    {
        var failures = new List<string>();
        failures.AddRange(ValidateProfile(baseline).Select(failure => $"Baseline: {failure}"));
        failures.AddRange(ValidateProfile(candidate).Select(failure => $"Candidate: {failure}"));
        if (baseline.Profile != DesktopPerformanceProfile.Stock)
        {
            failures.Add("The baseline must use the stock profile.");
        }

        if (candidate.Profile == DesktopPerformanceProfile.Stock)
        {
            failures.Add("The candidate must use compatibility or Lite profile.");
        }

        var baselineTotals = baseline.DesktopTotals;
        var candidateTotals = candidate.DesktopTotals;
        if (baselineTotals.PrivateBytes <= 0)
        {
            failures.Add("The baseline private-byte total must be greater than zero.");
        }
        else if (candidateTotals.PrivateBytes >
                 baselineTotals.PrivateBytes * MaximumPrivateBytesRatio)
        {
            failures.Add("Candidate private bytes did not beat stock by at least 10%.");
        }

        if (candidateTotals.CpuPercent >
            baselineTotals.CpuPercent + MaximumCpuIncreasePercentagePoints)
        {
            failures.Add("Candidate idle CPU exceeded the stock allowance.");
        }

        if (baselineTotals.HandleCount <= 0)
        {
            failures.Add("The baseline handle-count total must be greater than zero.");
        }
        else if (candidateTotals.HandleCount >
                 baselineTotals.HandleCount * MaximumHandleCountRatio)
        {
            failures.Add("Candidate handles exceeded the stock allowance.");
        }

        return new(
            failures.Count == 0,
            ReductionPercent(baselineTotals.PrivateBytes, candidateTotals.PrivateBytes),
            ReductionPercent(baselineTotals.WorkingSetBytes, candidateTotals.WorkingSetBytes),
            Math.Round(candidateTotals.CpuPercent - baselineTotals.CpuPercent, 4),
            ChangePercent(baselineTotals.HandleCount, candidateTotals.HandleCount),
            failures);
    }

    private static void RequireCerebrumProcesses(
        IReadOnlySet<string> names,
        ICollection<string> failures)
    {
        foreach (var required in RequiredCerebrumProcesses)
        {
            if (!names.Contains(required))
            {
                failures.Add($"The profile is missing required process {required}.");
            }
        }
    }

    private static double? ReductionPercent(long baseline, long candidate) =>
        baseline <= 0
            ? null
            : Math.Round((baseline - candidate) / (double)baseline * 100d, 2);

    private static double? ChangePercent(long baseline, long candidate) =>
        baseline <= 0
            ? null
            : Math.Round((candidate - baseline) / (double)baseline * 100d, 2);
}
