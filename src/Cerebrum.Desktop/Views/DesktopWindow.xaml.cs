using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Cerebrum.Desktop.Models;
using Cerebrum.Desktop.ViewModels;

namespace Cerebrum.Desktop.Views;

public partial class DesktopWindow : Window
{
    private const int GwlExStyle = -20;
    private const long WsExToolWindow = 0x00000080L;
    private const long WsExNoActivate = 0x08000000L;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;

    private readonly MonitorBounds _bounds;

    public DesktopWindow(MonitorBounds bounds, DesktopViewModel viewModel)
    {
        _bounds = bounds;
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
    }

    public DesktopViewModel ViewModel { get; }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var handle = new WindowInteropHelper(this).Handle;
        var extendedStyle = GetWindowLongPtr(handle, GwlExStyle).ToInt64();
        _ = SetWindowLongPtr(handle, GwlExStyle, new(extendedStyle | WsExToolWindow | WsExNoActivate));
        _ = SetWindowPos(
            handle,
            IntPtr.Zero,
            _bounds.Left,
            _bounds.Top,
            _bounds.Width,
            _bounds.Height,
            SwpNoZOrder | SwpNoActivate | SwpShowWindow);
    }

    protected override void OnClosed(EventArgs e)
    {
        ViewModel.Dispose();
        base.OnClosed(e);
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr newValue);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr window,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);
}
