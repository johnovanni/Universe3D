using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Universe3D.App;

/// <summary>Finds the display adapter that drives the monitor a window is on (Win32 monitor and display-device lookups).</summary>
internal static class MonitorAdapter
{
    private const uint MonitorDefaultToNearest = 2;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfoEx
    {
        public int Size;
        public int MonLeft, MonTop, MonRight, MonBottom, WorkLeft, WorkTop, WorkRight, WorkBottom;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Device;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DisplayDevice
    {
        public int Size;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
        public uint StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceId;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfoEx info);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplayDevices(string? device, uint index, ref DisplayDevice dd, uint flags);

    /// <summary>The monitor the window is on (an HMONITOR), or zero before the window has a handle.</summary>
    public static IntPtr MonitorOf(Window window)
    {
        IntPtr hwnd = new WindowInteropHelper(window).Handle;
        return hwnd == IntPtr.Zero ? IntPtr.Zero : MonitorFromWindow(hwnd, MonitorDefaultToNearest);
    }

    /// <summary>The adapter's device string (e.g. "Intel(R) Iris(R) Xe Graphics", "DisplayLink USB Device") for a monitor.</summary>
    public static string? AdapterString(IntPtr monitor)
    {
        if (monitor == IntPtr.Zero) return null;
        var info = new MonitorInfoEx { Size = Marshal.SizeOf<MonitorInfoEx>(), Device = "" };
        if (!GetMonitorInfo(monitor, ref info)) return null;
        for (uint i = 0; ; i++)
        {
            var dd = new DisplayDevice { Size = Marshal.SizeOf<DisplayDevice>(), DeviceName = "", DeviceString = "", DeviceId = "", DeviceKey = "" };
            if (!EnumDisplayDevices(null, i, ref dd, 0)) return null;
            if (string.Equals(dd.DeviceName, info.Device, StringComparison.OrdinalIgnoreCase)) return dd.DeviceString;
        }
    }
}
