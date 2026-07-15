using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using Cerebrum.Core.Components;

namespace Cerebrum.Desktop.ViewModels;

public sealed class ComponentTileViewModel : INotifyPropertyChanged
{
    private ComponentState _state;
    private string _detail;
    private Brush _indicatorBrush;

    public ComponentTileViewModel(ComponentDefinition definition)
    {
        Id = definition.Id;
        Name = definition.DisplayName;
        Role = definition.Role;
        _state = ComponentState.Unknown;
        _detail = "Waiting for session host";
        _indicatorBrush = BrushFor(_state);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ComponentId Id { get; }

    public string Name { get; }

    public string Role { get; }

    public string State => _state.ToString();

    public string Detail
    {
        get => _detail;
        private set => SetField(ref _detail, value);
    }

    public Brush IndicatorBrush
    {
        get => _indicatorBrush;
        private set => SetField(ref _indicatorBrush, value);
    }

    public void Update(ComponentStatus status)
    {
        if (_state != status.State)
        {
            _state = status.State;
            OnPropertyChanged(nameof(State));
            IndicatorBrush = BrushFor(_state);
        }

        Detail = status.Detail;
    }

    private static Brush BrushFor(ComponentState state) => state switch
    {
        ComponentState.Running => new SolidColorBrush(Color.FromRgb(74, 222, 128)),
        ComponentState.Starting => new SolidColorBrush(Color.FromRgb(125, 211, 252)),
        ComponentState.Missing => new SolidColorBrush(Color.FromRgb(251, 191, 36)),
        ComponentState.Failed => new SolidColorBrush(Color.FromRgb(248, 113, 113)),
        ComponentState.Stopped => new SolidColorBrush(Color.FromRgb(148, 163, 184)),
        _ => new SolidColorBrush(Color.FromRgb(100, 116, 139))
    };

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new(propertyName));
}
