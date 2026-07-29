using System.IO;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using Cerebrum.Core.Components;
using Cerebrum.Core.Configuration;
using Cerebrum.Core.Discovery;
using Cerebrum.Desktop.ViewModels;
using Cerebrum.Desktop.Views;
using Cerebrum.Host.Services;
using Microsoft.Win32;

namespace Cerebrum.Host;

public partial class App : Application
{
    private const string ExternalOrSystemShutdown = "EXTERNAL-OR-SYSTEM";

    private readonly List<DesktopWindow> _desktopWindows = [];
    private SingleInstanceCoordinator? _singleInstance;
    private DiagnosticLog? _diagnostics;
    private ComponentSupervisor? _supervisor;
    private CerebrumSettings? _settings;
    private bool _displayEventsAttached;
    private int _shutdownRequested;
    private string _shutdownReason = ExternalOrSystemShutdown;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _singleInstance = SingleInstanceCoordinator.Acquire();
        if (!_singleInstance.IsPrimary)
        {
            RequestShutdown("SECONDARY-INSTANCE", 0);
            return;
        }

        try
        {
            var dataRoot = DataRootResolver.Resolve();
            _diagnostics = new(dataRoot.Path);
            _diagnostics.Record("CER-HOST-START");
            if (dataRoot.RejectedOverride)
            {
                _diagnostics.Record("CER-DATA-ROOT-OVERRIDE-REJECTED");
            }

            RegisterExceptionDiagnostics();

            var settingsStore = new AtomicSettingsStore<CerebrumSettings>(
                Path.Combine(dataRoot.Path, "settings.json"),
                () => new(),
                CerebrumSettingsPolicy.IsValid);
            _settings = await settingsStore.LoadAsync().ConfigureAwait(true);
            if (!File.Exists(Path.Combine(dataRoot.Path, "settings.json")))
            {
                await settingsStore.SaveAsync(_settings).ConfigureAwait(true);
            }

            var repositoryRoot = RepositoryLocator.FindRepositoryRoot(AppContext.BaseDirectory);
#if DEBUG
            const string preferredConfiguration = "Debug";
#else
            const string preferredConfiguration = "Release";
#endif
            var resolver = new ComponentExecutableResolver(repositoryRoot, preferredConfiguration);
            var pipeName = CreateBrokerPipeName();
            _supervisor = new(_settings, resolver, pipeName, _diagnostics);
            _supervisor.StatusChanged += OnComponentStatusChanged;

            BuildDesktopSurfaces();
            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
            _displayEventsAttached = true;

            await _supervisor.StartSessionAsync().ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            _diagnostics?.Record("CER-HOST-START-FAILED", exception.GetType().Name);
            MessageBox.Show(
                "Cerebrum could not start its desktop session. Explorer has not been changed and remains available.",
                "Cerebrum startup",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            RequestShutdown("STARTUP-FAILED", 1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (Interlocked.CompareExchange(ref _shutdownRequested, 1, 0) == 0)
        {
            _shutdownReason = ExternalOrSystemShutdown;
            _diagnostics?.Record("CER-HOST-SHUTDOWN-REQUESTED", _shutdownReason);
        }

        if (_displayEventsAttached)
        {
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        }

        foreach (var window in _desktopWindows.ToArray())
        {
            window.Close();
        }

        if (_supervisor is not null)
        {
            _supervisor.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        if (_diagnostics is not null)
        {
            _diagnostics.Record("CER-HOST-STOP", _shutdownReason);
            _diagnostics.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        _singleInstance?.Dispose();
        base.OnExit(e);
    }

    private void BuildDesktopSurfaces()
    {
        foreach (var existing in _desktopWindows.ToArray())
        {
            existing.Close();
        }

        _desktopWindows.Clear();
        if (_settings?.StartWallpaper == true)
        {
            MainWindow = null;
            return;
        }

        foreach (var monitor in DisplayMonitorService.Enumerate())
        {
            var viewModel = new DesktopViewModel(
                monitor,
                _settings?.WallpaperPath,
                OpenFilesAsync,
                ShowOverviewAsync,
                CaptureRegionAsync,
                EnsureTaskbarAsync,
                RepairSessionAsync,
                () => RequestShutdown("USER-EXIT", 0));
            var window = new DesktopWindow(monitor, viewModel);
            _desktopWindows.Add(window);
            if (monitor.IsPrimary)
            {
                MainWindow = window;
            }

            window.Show();
        }
    }

    private Task OpenFilesAsync() => _supervisor?.OpenCortexAsync(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)) ?? Task.CompletedTask;

    private Task ShowOverviewAsync() =>
        _supervisor?.ShowThalamusOverviewAsync() ?? Task.CompletedTask;

    private Task CaptureRegionAsync() =>
        _supervisor?.CaptureSnipAsync(SnipCaptureMode.Region) ?? Task.CompletedTask;

    private Task EnsureTaskbarAsync() =>
        _supervisor?.EnsureRunningAsync(ComponentId.Medulla) ?? Task.CompletedTask;

    private Task RepairSessionAsync() =>
        _supervisor?.StartSessionAsync() ?? Task.CompletedTask;

    private void OnComponentStatusChanged(object? sender, ComponentStatus status)
    {
        _ = Dispatcher.BeginInvoke(() =>
        {
            foreach (var window in _desktopWindows)
            {
                window.ViewModel.Update(status);
            }
        }, DispatcherPriority.Background);
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        _ = Dispatcher.BeginInvoke(BuildDesktopSurfaces, DispatcherPriority.Background);
    }

    private void RegisterExceptionDiagnostics()
    {
        DispatcherUnhandledException += (_, args) =>
        {
            _diagnostics?.Record("CER-DISPATCHER-UNHANDLED", args.Exception.GetType().Name);
            args.Handled = true;
            _ = Dispatcher.BeginInvoke(
                () => RequestShutdown("DISPATCHER-FAILURE", 1),
                DispatcherPriority.Send);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            _diagnostics?.Record("CER-DOMAIN-UNHANDLED", args.ExceptionObject.GetType().Name);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            _diagnostics?.Record("CER-TASK-UNOBSERVED", args.Exception.GetType().Name);
            args.SetObserved();
        };
    }

    private void RequestShutdown(string reason, int exitCode)
    {
        if (Interlocked.Exchange(ref _shutdownRequested, 1) != 0)
        {
            return;
        }

        _shutdownReason = reason;
        _diagnostics?.Record("CER-HOST-SHUTDOWN-REQUESTED", reason);
        Shutdown(exitCode);
    }

    private static string CreateBrokerPipeName()
    {
        var identity = WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
        var identityHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))[..16];
        return $"Cerebrum.Broker.{Process.GetCurrentProcess().SessionId}.{identityHash}";
    }
}
