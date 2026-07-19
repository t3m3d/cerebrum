using System.ComponentModel;
using System.Diagnostics;

namespace Cerebrum.Core.Performance;

public sealed class DesktopResourceCollector
{
    private static readonly TimeSpan MinimumDuration = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaximumDuration = TimeSpan.FromMinutes(5);

    public async Task<DesktopResourceSnapshot> CaptureAsync(
        DesktopPerformanceProfile profile,
        TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        if (duration < MinimumDuration || duration > MaximumDuration)
        {
            throw new ArgumentOutOfRangeException(
                nameof(duration),
                "The sample duration must be between one second and five minutes.");
        }

        var capturedAtUtc = DateTimeOffset.UtcNow;
        var initial = ObserveCurrentSession();
        var stopwatch = Stopwatch.StartNew();
        await Task.Delay(duration, cancellationToken).ConfigureAwait(false);
        stopwatch.Stop();
        var final = ObserveCurrentSession();
        var logicalProcessorCount = Environment.ProcessorCount;

        var samples = new List<DesktopProcessSample>(final.Count);
        foreach (var observation in final.Values)
        {
            var cpuSampled =
                initial.TryGetValue(observation.ProcessId, out var starting) &&
                string.Equals(starting.ProcessName, observation.ProcessName, StringComparison.OrdinalIgnoreCase) &&
                observation.ProcessorTime >= starting.ProcessorTime;
            var cpuPercent = cpuSampled
                ? (observation.ProcessorTime - starting!.ProcessorTime).TotalMilliseconds /
                    (stopwatch.Elapsed.TotalMilliseconds * logicalProcessorCount) * 100d
                : 0d;
            samples.Add(new(
                observation.ProcessId,
                observation.ProcessName,
                observation.Group,
                observation.PrivateBytes,
                observation.WorkingSetBytes,
                observation.HandleCount,
                observation.ThreadCount,
                Math.Round(cpuPercent, 4),
                cpuSampled));
        }

        foreach (var observation in initial.Values.Where(
                     starting => !final.ContainsKey(starting.ProcessId)))
        {
            samples.Add(new(
                observation.ProcessId,
                observation.ProcessName,
                observation.Group,
                observation.PrivateBytes,
                observation.WorkingSetBytes,
                observation.HandleCount,
                observation.ThreadCount,
                CpuPercent: 0d,
                CpuSampled: false));
        }

        return DesktopResourceSnapshot.Create(
            profile,
            capturedAtUtc,
            stopwatch.Elapsed,
            logicalProcessorCount,
            samples);
    }

    private static Dictionary<int, ProcessObservation> ObserveCurrentSession()
    {
        using var currentProcess = Process.GetCurrentProcess();
        var currentSessionId = currentProcess.SessionId;
        var observations = new Dictionary<int, ProcessObservation>();
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                var processName = process.ProcessName;
                if (process.SessionId != currentSessionId ||
                    !DesktopProcessCatalog.TryGetGroup(processName, out var group))
                {
                    continue;
                }

                process.Refresh();
                observations[process.Id] = new(
                    process.Id,
                    processName,
                    group,
                    process.TotalProcessorTime,
                    process.PrivateMemorySize64,
                    process.WorkingSet64,
                    process.HandleCount,
                    process.Threads.Count);
            }
            catch (Exception exception) when (
                exception is InvalidOperationException or
                Win32Exception or
                NotSupportedException)
            {
                // A process can exit or become inaccessible while the snapshot is taken.
            }
            finally
            {
                process.Dispose();
            }
        }

        return observations;
    }

    private sealed record ProcessObservation(
        int ProcessId,
        string ProcessName,
        DesktopProcessGroup Group,
        TimeSpan ProcessorTime,
        long PrivateBytes,
        long WorkingSetBytes,
        int HandleCount,
        int ThreadCount);
}
