using System.Runtime.InteropServices;
using Cerebrum.Desktop.Models;

namespace Cerebrum.Host.Services;

internal static class DisplayMonitorService
{
    private const uint MonitorInfoPrimary = 0x00000001;

    public static IReadOnlyList<MonitorBounds> Enumerate()
    {
        var monitors = new List<MonitorBounds>();
        _ = EnumDisplayMonitors(
            IntPtr.Zero,
            IntPtr.Zero,
            (monitor, _, _, _) =>
            {
                var info = new MonitorInfoEx
                {
                    Size = Marshal.SizeOf<MonitorInfoEx>()
                };
                if (GetMonitorInfo(monitor, ref info))
                {
                    monitors.Add(new(
                        string.IsNullOrWhiteSpace(info.DeviceName) ? "Display" : info.DeviceName,
                        info.Monitor.Left,
                        info.Monitor.Top,
                        info.Monitor.Right - info.Monitor.Left,
                        info.Monitor.Bottom - info.Monitor.Top,
                        (info.Flags & MonitorInfoPrimary) != 0));
                }

                return true;
            },
            IntPtr.Zero);

        if (monitors.Count == 0)
        {
            monitors.Add(new("Primary display", 0, 0, 1280, 720, IsPrimary: true));
        }

        return monitors
            .OrderByDescending(monitor => monitor.IsPrimary)
            .ThenBy(monitor => monitor.Left)
            .ThenBy(monitor => monitor.Top)
            .ToArray();
    }

    private delegate bool MonitorEnumerationCallback(
        IntPtr monitor,
        IntPtr deviceContext,
        IntPtr monitorRectangle,
        IntPtr data);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfoEx
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect WorkArea;
        public uint Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayMonitors(
        IntPtr deviceContext,
        IntPtr clipRectangle,
        MonitorEnumerationCallback callback,
        IntPtr data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfoEx monitorInfo);
}
