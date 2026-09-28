using System;
using System.Runtime.InteropServices;
using System.Windows;

namespace AlatStatistik;

internal static class PenempatanJendela
{
    public static void PusatkanDiLayarAktif(this Window jendela, Window? pemilik = null)
    {
        if (pemilik != null)
        {
            jendela.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            return;
        }

        jendela.WindowStartupLocation = WindowStartupLocation.Manual;
        // SourceInitialized: atur posisi sebelum jendela tampak supaya tidak
        // berkedip sebentar di (0,0).
        jendela.SourceInitialized += (_, _) =>
        {
            RECT? area = LayarAktif();
            if (area is null) return;

            double lebar = !double.IsNaN(jendela.Width) ? jendela.Width : jendela.ActualWidth;
            double tinggi = !double.IsNaN(jendela.Height) ? jendela.Height : jendela.ActualHeight;
            double areaLebar = area.Value.Right - area.Value.Left;
            double areaTinggi = area.Value.Bottom - area.Value.Top;

            jendela.Left = area.Value.Left + Math.Max(0, (areaLebar - lebar) / 2);
            jendela.Top = area.Value.Top + Math.Max(0, (areaTinggi - tinggi) / 2);
        };
    }

    private static RECT? LayarAktif()
    {
        if (!GetCursorPos(out POINT titik))
        {
            titik = new POINT
            {
                X = (int)(SystemParameters.PrimaryScreenWidth / 2),
                Y = (int)(SystemParameters.PrimaryScreenHeight / 2)
            };
        }

        IntPtr monitor = MonitorFromPoint(titik, MONITOR_DEFAULTTONEAREST);
        if (monitor == IntPtr.Zero) return null;

        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        return GetMonitorInfo(monitor, ref info) ? info.rcWork : null;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    private const uint MONITOR_DEFAULTTONEAREST = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }
}
