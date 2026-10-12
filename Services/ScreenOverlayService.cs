using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace DynamicIslandWin.Services;

public sealed class ScreenOverlayService
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    private Window? _overlayWindow;
    private int _brightnessPercent = 100; // 25..100
    private bool _warmNightMode;

    public int BrightnessPercent => _brightnessPercent;
    public bool WarmNightMode => _warmNightMode;

    public void SetState(int brightnessPercent, bool warmNightMode)
    {
        _brightnessPercent = Math.Clamp(brightnessPercent, 25, 100);
        _warmNightMode = warmNightMode;
        ApplyVisual();
    }

    public void CloseOverlay()
    {
        try
        {
            _overlayWindow?.Close();
            _overlayWindow = null;
        }
        catch
        {
        }
    }

    private void EnsureOverlayWindow()
    {
        if (_overlayWindow != null) return;

        _overlayWindow = new Window
        {
            Title = "DynamicIslandScreenFilter",
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            Topmost = true,
            ShowInTaskbar = false,
            ShowActivated = false,
            Focusable = false,
            IsHitTestVisible = false,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = SystemParameters.VirtualScreenLeft,
            Top = SystemParameters.VirtualScreenTop,
            Width = SystemParameters.VirtualScreenWidth,
            Height = SystemParameters.VirtualScreenHeight
        };

        _overlayWindow.SourceInitialized += (_, _) =>
        {
            var helper = new WindowInteropHelper(_overlayWindow);
            int ex = GetWindowLong(helper.Handle, GWL_EXSTYLE);
            SetWindowLong(helper.Handle, GWL_EXSTYLE, ex | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
        };
    }

    private void ApplyVisual()
    {
        bool needsOverlay = _brightnessPercent < 99 || _warmNightMode;
        if (!needsOverlay)
        {
            if (_overlayWindow is { IsVisible: true })
            {
                _overlayWindow.Hide();
            }
            return;
        }

        EnsureOverlayWindow();
        if (_overlayWindow == null) return;

        // Calculate dim alpha (0..185) and warm amber mix
        double dimFactor = (100 - _brightnessPercent) / 100.0; // 0.0 to 0.75
        int dimAlpha = (int)Math.Round(dimFactor * 205);

        Color filterColor;
        if (_warmNightMode)
        {
            // Combine warm amber night light with dimming
            int warmAlpha = Math.Clamp(dimAlpha + 48, 45, 215);
            byte r = (byte)Math.Round(42 * (1.0 - dimFactor * 0.7));
            byte g = (byte)Math.Round(20 * (1.0 - dimFactor * 0.7));
            byte b = 0;
            filterColor = Color.FromArgb((byte)warmAlpha, r, g, b);
        }
        else
        {
            filterColor = Color.FromArgb((byte)Math.Clamp(dimAlpha, 0, 210), 0, 0, 0);
        }

        _overlayWindow.Background = new SolidColorBrush(filterColor);

        if (!_overlayWindow.IsVisible)
        {
            _overlayWindow.Show();
        }
    }
}
