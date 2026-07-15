using System.Text.Json;

namespace Cerebrum.Core.Configuration;

public sealed class AtomicSettingsStore<T>
    where T : class
{
    private const long MaximumDocumentBytes = 1024 * 1024;

    private readonly string _path;
    private readonly string _backupPath;
    private readonly Func<T> _createDefault;
    private readonly Func<T, bool> _validate;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public AtomicSettingsStore(string path, Func<T> createDefault, Func<T, bool> validate)
    {
        _path = Path.GetFullPath(path);
        _backupPath = _path + ".bak";
        _createDefault = createDefault;
        _validate = validate;
    }

    public async Task<T> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var current = await TryReadAsync(_path, cancellationToken).ConfigureAwait(false);
            if (current is not null)
            {
                return current;
            }

            var backup = await TryReadAsync(_backupPath, cancellationToken).ConfigureAwait(false);
            return backup ?? _createDefault();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(T value, CancellationToken cancellationToken = default)
    {
        if (!_validate(value))
        {
            throw new InvalidDataException("The settings value failed semantic validation.");
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await SaveCoreAsync(value, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<T?> TryReadAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            var file = new FileInfo(path);
            if (!file.Exists || file.Length is <= 0 or > MaximumDocumentBytes)
            {
                return null;
            }

            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 16 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var value = await JsonSerializer.DeserializeAsync<T>(stream, _jsonOptions, cancellationToken)
                .ConfigureAwait(false);
            return value is not null && _validate(value) ? value : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private async Task SaveCoreAsync(T value, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_path)
            ?? throw new InvalidOperationException("The settings path has no parent directory.");
        Directory.CreateDirectory(directory);

        var temporaryPath = Path.Combine(
            directory,
            $".{Path.GetFileName(_path)}.{Guid.NewGuid():N}.pending");

        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 16 * 1024,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, value, _jsonOptions, cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(_path))
            {
                try
                {
                    File.Replace(temporaryPath, _path, _backupPath, ignoreMetadataErrors: true);
                }
                catch (Exception exception) when (exception is IOException or PlatformNotSupportedException)
                {
                    File.Copy(_path, _backupPath, overwrite: true);
                    File.Move(temporaryPath, _path, overwrite: true);
                }
            }
            else
            {
                File.Move(temporaryPath, _path);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
