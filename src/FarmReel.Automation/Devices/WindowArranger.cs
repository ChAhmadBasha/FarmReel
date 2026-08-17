using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace FarmReel.Automation.Devices
{
    /// <summary>Arranges emulator windows into rows/columns on a chosen screen (Win32).</summary>
    public static class WindowArranger
    {
        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        [DllImport("user32.dll")] private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool MoveWindow(IntPtr hWnd, int x, int y, int w, int h, bool repaint);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
        [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint flags);
        [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        /// <summary>Find emulator window handles by title keywords (LDPlayer / MuMu).</summary>
        public static List<IntPtr> FindEmulatorWindows(params string[] titleKeywords)
        {
            var result = new List<IntPtr>();
            EnumWindows((hWnd, lParam) =>
            {
                if (!IsWindowVisible(hWnd)) return true;
                var sb = new StringBuilder(256);
                GetWindowText(hWnd, sb, sb.Capacity);
                var title = sb.ToString();
                foreach (var kw in titleKeywords)
                {
                    if (title.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        result.Add(hWnd);
                        break;
                    }
                }
                return true;
            }, IntPtr.Zero);
            return result;
        }

        /// <summary>Arrange windows into a grid. screenIndex 0 = primary.</summary>
        public static void Arrange(IReadOnlyList<IntPtr> windows, int screenIndex, int columns)
        {
            if (windows == null || windows.Count == 0) return;
            var mon = GetMonitorInfo(screenIndex);
            if (mon == null) return;
            var work = mon.Value.rcWork;
            int width = work.Right - work.Left;
            int height = work.Bottom - work.Top;
            if (columns <= 0) columns = (int)Math.Ceiling(Math.Sqrt(windows.Count));
            int cellW = width / columns;
            int rows = (int)Math.Ceiling((double)windows.Count / columns);
            int cellH = height / rows;

            for (int i = 0; i < windows.Count; i++)
            {
                int col = i % columns;
                int row = i / columns;
                MoveWindow(windows[i],
                    work.Left + col * cellW + 4,
                    work.Top + row * cellH + 4,
                    cellW - 8, cellH - 8, true);
            }
        }

        public static RECT? GetMonitorInfo(int index)
        {
            var hMon = MonitorFromWindow(IntPtr.Zero, 0x2 /*MONITOR_DEFAULTTONEAREST*/);
            var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (!GetMonitorInfo(hMon, ref mi)) return null;
            // Index > 0 needs full monitor enumeration; keep primary for now and document.
            return mi;
        }
    }
}
