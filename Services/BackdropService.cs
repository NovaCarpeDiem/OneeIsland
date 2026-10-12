using System;
using System.Runtime.InteropServices;

namespace DynamicIslandWin.Services;

public static class BackdropService
{
    [DllImport("dwmapi.dll", SetLastError = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

    private const int DWMWCP_DONOTROUND = 1;
    private const int DWMSBT_NONE = 1;

    public static bool IsWindows11_22H2OrGreater => Environment.OSVersion.Version.Build >= 22621;

    /// <summary>
    /// Explicitly tells Windows DWM to NEVER render a window frame, border, shadow,
    /// or rectangular backdrop over the transparent WPF window bounds.
    /// </summary>
    public static void ClearWindowFrameAndBackdrop(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;

        try
        {
            if (IsWindows11_22H2OrGreater)
            {
                int none = DWMSBT_NONE;
                DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref none, sizeof(int));
            }

            int doNotRound = DWMWCP_DONOTROUND;
            DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref doNotRound, sizeof(int));
        }
        catch
        {
        }
    }
}
