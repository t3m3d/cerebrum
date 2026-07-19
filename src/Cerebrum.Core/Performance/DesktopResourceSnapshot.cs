namespace Cerebrum.Core.Performance;

public enum DesktopPerformanceProfile
{
    Stock,
    Compatibility,
    Lite
}

public sealed record DesktopProcessSample(
    int ProcessId,
    string ProcessName,
    DesktopProcessGroup Group,
    long PrivateBytes,
    long WorkingSetBytes,
    int HandleCount,
    int ThreadCount,
    double CpuPercent,
    bool CpuSampled);

public sealed record DesktopResourceTotals(
    int ProcessCount,
    long PrivateBytes,
    long WorkingSetBytes,
    int HandleCount,
    int ThreadCount,
    double CpuPercent)
{
    public static DesktopResourceTotals Sum(IEnumerable<DesktopProcessSample> samples)
    {
        var materialized = samples.ToArray();
        return new(
            materialized.Length,
            materialized.Sum(sample => sample.PrivateBytes),
            materialized.Sum(sample => sample.WorkingSetBytes),
            materialized.Sum(sample => sample.HandleCount),
            materialized.Sum(sample => sample.ThreadCount),
            Math.Round(materialized.Sum(sample => sample.CpuPercent), 4));
    }
}

public sealed record DesktopResourceSnapshot(
    int SchemaVersion,
    DesktopPerformanceProfile Profile,
    DateTimeOffset CapturedAtUtc,
    double SampleDurationSeconds,
    int LogicalProcessorCount,
    IReadOnlyList<DesktopProcessSample> Processes,
    DesktopResourceTotals DesktopTotals,
    DesktopResourceTotals CompositorTotals)
{
    public const int CurrentSchemaVersion = 1;

    public static DesktopResourceSnapshot Create(
        DesktopPerformanceProfile profile,
        DateTimeOffset capturedAtUtc,
        TimeSpan sampleDuration,
        int logicalProcessorCount,
        IEnumerable<DesktopProcessSample> processes)
    {
        var ordered = processes
            .OrderBy(sample => sample.Group)
            .ThenBy(sample => sample.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(sample => sample.ProcessId)
            .ToArray();
        var desktop = ordered.Where(sample => sample.Group != DesktopProcessGroup.Compositor);
        var compositor = ordered.Where(sample => sample.Group == DesktopProcessGroup.Compositor);
        return new(
            CurrentSchemaVersion,
            profile,
            capturedAtUtc,
            Math.Round(sampleDuration.TotalSeconds, 3),
            logicalProcessorCount,
            ordered,
            DesktopResourceTotals.Sum(desktop),
            DesktopResourceTotals.Sum(compositor));
    }
}
