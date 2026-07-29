using System.IO;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Threading;
using Cerebrum.Core.Components;
using Cerebrum.Desktop.Models;

namespace Cerebrum.Desktop.ViewModels;

public sealed class DesktopViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly DispatcherTimer _clockTimer;
    private string _clockText = string.Empty;
    private string _dateText = string.Empty;
    private string _sessionSummary = "Starting the Cerebrum session";

    public DesktopViewModel(
        MonitorBounds monitor,
        string? wallpaperPath,
        Func<Task> openFiles,
        Func<Task> showOverview,
        Func<Task> captureRegion,
        Func<Task> ensureTaskbar,
        Func<Task> repairSession,
        Action exit)
    {
        MonitorName = monitor.IsPrimary ? "Primary display" : monitor.DeviceName;
        DisplayBadge = monitor.IsPrimary ? "PRIMARY" : "DISPLAY";
        WallpaperPath = !string.IsNullOrWhiteSpace(wallpaperPath) && File.Exists(wallpaperPath)
            ? wallpaperPath
            : null;

        Components = new(ComponentCatalog.All.Select(definition => new ComponentTileViewModel(definition)));
        OpenFilesCommand = new AsyncRelayCommand(openFiles);
        ShowOverviewCommand = new AsyncRelayCommand(showOverview);
        CaptureRegionCommand = new AsyncRelayCommand(captureRegion);
        EnsureTaskbarCommand = new AsyncRelayCommand(ensureTaskbar);
        RepairSessionCommand = new AsyncRelayCommand(repairSession);
        ExitCommand = new RelayCommand(exit);

        UpdateClock();
        _clockTimer = new DispatcherTimer(
            TimeSpan.FromSeconds(1),
            DispatcherPriority.Background,
            (_, _) => UpdateClock(),
            Dispatcher.CurrentDispatcher);
        _clockTimer.Start();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string MonitorName { get; }

    public string DisplayBadge { get; }

    public string? WallpaperPath { get; }

    public ObservableCollection<ComponentTileViewModel> Components { get; }

    public ICommand OpenFilesCommand { get; }

    public ICommand ShowOverviewCommand { get; }

    public ICommand CaptureRegionCommand { get; }

    public ICommand EnsureTaskbarCommand { get; }

    public ICommand RepairSessionCommand { get; }

    public ICommand ExitCommand { get; }

    public string ClockText
    {
        get => _clockText;
        private set => SetField(ref _clockText, value);
    }

    public string DateText
    {
        get => _dateText;
        private set => SetField(ref _dateText, value);
    }

    public string SessionSummary
    {
        get => _sessionSummary;
        private set => SetField(ref _sessionSummary, value);
    }

    public void Update(ComponentStatus status)
    {
        Components.FirstOrDefault(component => component.Id == status.Id)?.Update(status);

        var running = Components.Count(component => component.State == ComponentState.Running.ToString());
        var missing = Components.Count(component => component.State == ComponentState.Missing.ToString());
        SessionSummary = missing > 0
            ? $"{running} components online · {missing} build missing"
            : $"{running} components online · compatibility mode";
    }

    public void Dispose() => _clockTimer.Stop();

    private void UpdateClock()
    {
        var now = DateTime.Now;
        ClockText = now.ToString("h:mm tt");
        DateText = now.ToString("dddd, MMMM d");
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new(propertyName));
    }
}
