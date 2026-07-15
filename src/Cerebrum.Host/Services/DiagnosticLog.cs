using System.IO;
using System.Text;
using System.Threading.Channels;

namespace Cerebrum.Host.Services;

internal sealed class DiagnosticLog : IAsyncDisposable
{
    private const long RotateAtBytes = 1024 * 1024;

    private readonly string _path;
    private readonly Channel<LogEntry> _entries = Channel.CreateUnbounded<LogEntry>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    private readonly Task _writerTask;

    public DiagnosticLog(string dataRoot)
    {
        _path = Path.Combine(dataRoot, "logs", "host.log");
        _writerTask = Task.Run(WriteLoopAsync);
    }

    public void Record(string eventCode, string? safeDetail = null)
    {
        _entries.Writer.TryWrite(new(DateTimeOffset.UtcNow, eventCode, safeDetail));
    }

    public async ValueTask DisposeAsync()
    {
        _entries.Writer.TryComplete();
        await _writerTask.ConfigureAwait(false);
    }

    private async Task WriteLoopAsync()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            await foreach (var entry in _entries.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                RotateIfNeeded();
                var line = new StringBuilder()
                    .Append(entry.Timestamp.ToString("O"))
                    .Append(' ')
                    .Append(entry.EventCode);
                if (!string.IsNullOrWhiteSpace(entry.SafeDetail))
                {
                    line.Append(' ').Append(entry.SafeDetail);
                }

                line.AppendLine();
                await File.AppendAllTextAsync(_path, line.ToString(), Encoding.UTF8).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Diagnostics must never take down the desktop session.
        }
    }

    private void RotateIfNeeded()
    {
        var current = new FileInfo(_path);
        if (!current.Exists || current.Length < RotateAtBytes)
        {
            return;
        }

        var previous = _path + ".1";
        File.Move(_path, previous, overwrite: true);
    }

    private sealed record LogEntry(DateTimeOffset Timestamp, string EventCode, string? SafeDetail);
}
