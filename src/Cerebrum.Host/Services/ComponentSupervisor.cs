using System.IO;
using System.Collections.Concurrent;
using System.Diagnostics;
using Cerebrum.Core.Components;
using Cerebrum.Core.Configuration;
using Cerebrum.Core.Discovery;
using Cerebrum.Core.Protocol;

namespace Cerebrum.Host.Services;

internal sealed class ComponentSupervisor : IAsyncDisposable
{
    private static readonly TimeSpan RestartWindow = TimeSpan.FromMinutes(1);

    private readonly CerebrumSettings _settings;
    private readonly ComponentExecutableResolver _resolver;
    private readonly BrokerClient _brokerClient;
    private readonly string _brokerPipeName;
    private readonly DiagnosticLog _diagnostics;
    private readonly ConcurrentDictionary<ComponentId, Process> _ownedProcesses = new();
    private readonly ConcurrentDictionary<ComponentId, Queue<DateTimeOffset>> _restartHistory = new();
    private readonly ConcurrentDictionary<ComponentId, SemaphoreSlim> _componentGates = new();
    private readonly SemaphoreSlim _sessionGate = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private volatile bool _shuttingDown;

    public ComponentSupervisor(
        CerebrumSettings settings,
        ComponentExecutableResolver resolver,
        string brokerPipeName,
        DiagnosticLog diagnostics)
    {
        _settings = settings;
        _resolver = resolver;
        _brokerPipeName = brokerPipeName;
        _brokerClient = new(brokerPipeName);
        _diagnostics = diagnostics;
    }

    public event EventHandler<ComponentStatus>? StatusChanged;

    public async Task StartSessionAsync(CancellationToken cancellationToken = default)
    {
        await _sessionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureRunningAsync(ComponentId.Broker, cancellationToken).ConfigureAwait(false);
            if (_settings.StartMedulla)
            {
                await EnsureRunningAsync(ComponentId.Medulla, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                Publish(ComponentId.Medulla, ComponentState.Stopped, "Disabled in Cerebrum settings");
            }

            if (_settings.StartThalamus)
            {
                await EnsureRunningAsync(ComponentId.Thalamus, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                Publish(ComponentId.Thalamus, ComponentState.Stopped, "Disabled in Cerebrum settings");
            }

            var cortex = await Task.Run(
                () => _resolver.Resolve(ComponentCatalog.Get(ComponentId.Cortex), _settings), cancellationToken).ConfigureAwait(false);
            Publish(
                ComponentId.Cortex,
                cortex is null ? ComponentState.Missing : ComponentState.Stopped,
                cortex is null ? "Executable not found" : "Available on demand");
        }
        finally
        {
            _sessionGate.Release();
        }
    }

    public async Task EnsureRunningAsync(ComponentId id, CancellationToken cancellationToken = default)
    {
        var gate = _componentGates.GetOrAdd(id, _ => new(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_ownedProcesses.TryGetValue(id, out var owned) && !owned.HasExited)
            {
                Publish(id, ComponentState.Running, id == ComponentId.Broker ? "Private broker online" : "Managed by Cerebrum");
                return;
            }

            var definition = ComponentCatalog.Get(id);
            using var existingProcess = definition.IsInternal ? null : await Task.Run(() => FindExistingProcess(definition), cancellationToken).ConfigureAwait(false);
            if (existingProcess is not null)
            {
                Publish(id, ComponentState.Running, "Running independently");
                return;
            }

            var resolved = await Task.Run(
                () => _resolver.Resolve(definition, _settings), cancellationToken).ConfigureAwait(false);
            if (resolved is null)
            {
                Publish(id, ComponentState.Missing, "Executable not found");
                _diagnostics.Record("CER-COMPONENT-MISSING", id.ToString().ToUpperInvariant());
                return;
            }

            Publish(id, ComponentState.Starting, $"Starting from {resolved.Source}");
            var arguments = id == ComponentId.Broker
                ? new[] { "--serve", "--pipe", _brokerPipeName }
                : Array.Empty<string>();
            var process = StartProcess(resolved.Path, arguments);
            process.EnableRaisingEvents = true;
            process.Exited += (_, _) => _ = HandleUnexpectedExitAsync(id, process);
            _ownedProcesses[id] = process;

            if (id == ComponentId.Broker)
            {
                var healthy = await WaitForBrokerAsync(cancellationToken).ConfigureAwait(false);
                Publish(
                    id,
                    healthy ? ComponentState.Running : ComponentState.Failed,
                    healthy ? "Private broker healthy" : "Broker health deadline expired");
                return;
            }

            Publish(id, ComponentState.Running, $"Started from {resolved.Source}");
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            Publish(id, ComponentState.Failed, "Process launch failed");
            _diagnostics.Record("CER-COMPONENT-START-FAILED", id.ToString().ToUpperInvariant());
        }
        finally
        {
            gate.Release();
        }
    }

    public Task OpenCortexAsync(string path, CancellationToken cancellationToken = default) =>
        InvokeAsync(ComponentId.Cortex, ["--open", path], cancellationToken);

    public Task ShowThalamusOverviewAsync(CancellationToken cancellationToken = default) =>
        InvokeAsync(ComponentId.Thalamus, ["--overview"], cancellationToken);

    public async Task InvokeAsync(
        ComponentId id,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var definition = ComponentCatalog.Get(id);
        var resolved = await Task.Run(
            () => _resolver.Resolve(definition, _settings), cancellationToken).ConfigureAwait(false);
        if (resolved is null)
        {
            Publish(id, ComponentState.Missing, "Executable not found");
            return;
        }

        try
        {
            using var process = StartProcess(resolved.Path, arguments);
            Publish(id, ComponentState.Running, "Command sent");
            await Task.Yield();
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            Publish(id, ComponentState.Failed, "Command launch failed");
            _diagnostics.Record("CER-COMPONENT-COMMAND-FAILED", id.ToString().ToUpperInvariant());
        }
    }

    public async ValueTask DisposeAsync()
    {
        _shuttingDown = true;
        _lifetime.Cancel();

        if (_ownedProcesses.ContainsKey(ComponentId.Broker))
        {
            _ = await _brokerClient.SendAsync(
                BrokerProtocol.ShutdownCommand,
                TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        }

        foreach (var process in _ownedProcesses.Values)
        {
            process.Dispose();
        }

        foreach (var gate in _componentGates.Values)
        {
            gate.Dispose();
        }

        _sessionGate.Dispose();
        _lifetime.Dispose();
    }

    private async Task<bool> WaitForBrokerAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 12; attempt++)
        {
            var response = await _brokerClient.SendAsync(
                BrokerProtocol.HealthCommand,
                TimeSpan.FromMilliseconds(500),
                cancellationToken).ConfigureAwait(false);
            if (response is { Success: true, Status: "healthy" })
            {
                return true;
            }

            await Task.Delay(150, cancellationToken).ConfigureAwait(false);
        }

        return false;
    }

    private async Task HandleUnexpectedExitAsync(ComponentId id, Process process)
    {
        _ownedProcesses.TryRemove(new(id, process));
        process.Dispose();
        if (_shuttingDown)
        {
            return;
        }

        Publish(id, ComponentState.Stopped, "Process exited unexpectedly");
        _diagnostics.Record("CER-COMPONENT-EXITED", id.ToString().ToUpperInvariant());

        var definition = ComponentCatalog.Get(id);
        if (!definition.RestartAfterUnexpectedExit || !_settings.RestartSessionComponents)
        {
            return;
        }

        var history = _restartHistory.GetOrAdd(id, _ => new());
        int attempt;
        lock (history)
        {
            var cutoff = DateTimeOffset.UtcNow - RestartWindow;
            while (history.Count > 0 && history.Peek() < cutoff)
            {
                history.Dequeue();
            }

            if (history.Count >= _settings.RestartLimit)
            {
                Publish(id, ComponentState.Failed, "Restart limit reached; use session repair");
                _diagnostics.Record("CER-COMPONENT-RESTART-LIMIT", id.ToString().ToUpperInvariant());
                return;
            }

            history.Enqueue(DateTimeOffset.UtcNow);
            attempt = history.Count;
        }

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(attempt), _lifetime.Token).ConfigureAwait(false);
            await EnsureRunningAsync(id, _lifetime.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
            // The desktop is shutting down.
        }
    }

    private static Process StartProcess(string executablePath, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = Path.GetDirectoryName(executablePath)!,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return Process.Start(startInfo)
            ?? throw new InvalidOperationException("Windows did not create the component process.");
    }

    private static Process? FindExistingProcess(ComponentDefinition definition)
    {
        var processName = Path.GetFileNameWithoutExtension(definition.ExecutableName);
        var currentSession = Process.GetCurrentProcess().SessionId;
        foreach (var process in Process.GetProcessesByName(processName))
        {
            try
            {
                if (!process.HasExited && process.SessionId == currentSession)
                {
                    return process;
                }
            }
            catch (InvalidOperationException)
            {
                // The process exited during discovery.
            }

            process.Dispose();
        }

        return null;
    }

    private void Publish(ComponentId id, ComponentState state, string detail)
    {
        var definition = ComponentCatalog.Get(id);
        StatusChanged?.Invoke(
            this,
            new(id, definition.DisplayName, state, detail, DateTimeOffset.UtcNow));
    }
}
