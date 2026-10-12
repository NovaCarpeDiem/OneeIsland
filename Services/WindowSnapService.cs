using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using Forms = System.Windows.Forms;

namespace DynamicIslandWin.Services;

public enum WindowSnapMode
{
    LeftHalf,
    RightHalf,
    TopHalf,
    BottomHalf,
    Maximize,
    CenterFloat,
    LeftThird,
    RightTwoThirds
}

public static class WindowSnapService
{
    private const int SW_RESTORE = 9;
    private const int SW_MAXIMIZE = 3;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_SHOWWINDOW = 0x0040;

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool IsZoomed(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    public static string GetWindowTitle(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero) return "Pencere Seçilmedi";
        try
        {
            int length = GetWindowTextLength(hWnd);
            if (length == 0) return "Aktif Pencere";
            var builder = new StringBuilder(length + 1);
            GetWindowText(hWnd, builder, builder.Capacity);
            string title = builder.ToString().Trim();
            return string.IsNullOrWhiteSpace(title) ? "Aktif Pencere" : title;
        }
        catch
        {
            return "Aktif Pencere";
        }
    }

    public static bool SnapWindow(IntPtr hWnd, WindowSnapMode mode)
    {
        if (hWnd == IntPtr.Zero) return false;

        try
        {
            // If iconic (minimized), restore it first
            if (IsIconic(hWnd))
            {
                ShowWindow(hWnd, SW_RESTORE);
            }

            if (mode == WindowSnapMode.Maximize)
            {
                ShowWindow(hWnd, SW_MAXIMIZE);
                SetForegroundWindow(hWnd);
                return true;
            }

            // Restore from maximize if currently zoomed
            if (IsZoomed(hWnd))
            {
                ShowWindow(hWnd, SW_RESTORE);
            }

            // Get target monitor working area (respects taskbar position)
            var screen = Forms.Screen.FromHandle(hWnd);
            Rectangle work = screen.WorkingArea;

            int x = work.Left;
            int y = work.Top;
            int w = work.Width;
            int h = work.Height;

            switch (mode)
            {
                case WindowSnapMode.LeftHalf:
                    w = work.Width / 2;
                    break;

                case WindowSnapMode.RightHalf:
                    x = work.Left + (work.Width / 2);
                    w = work.Width - (work.Width / 2);
                    break;

                case WindowSnapMode.TopHalf:
                    h = work.Height / 2;
                    break;

                case WindowSnapMode.BottomHalf:
                    y = work.Top + (work.Height / 2);
                    h = work.Height - (work.Height / 2);
                    break;

                case WindowSnapMode.CenterFloat:
                    w = (int)(work.Width * 0.72);
                    h = (int)(work.Height * 0.78);
                    x = work.Left + ((work.Width - w) / 2);
                    y = work.Top + ((work.Height - h) / 2);
                    break;

                case WindowSnapMode.LeftThird:
                    w = work.Width / 3;
                    break;

                case WindowSnapMode.RightTwoThirds:
                    x = work.Left + (work.Width / 3);
                    w = work.Width - (work.Width / 3);
                    break;
            }

            SetWindowPos(hWnd, IntPtr.Zero, x, y, w, h, SWP_NOZORDER | SWP_SHOWWINDOW);
            SetForegroundWindow(hWnd);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
