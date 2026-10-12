using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DynamicIslandWin.Services;
using Forms = System.Windows.Forms;

namespace DynamicIslandWin;

public partial class MainWindow : Window
{
    #region Win32 Constants & Interop
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int VK_CONTROL = 0x11;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [StructLayout(LayoutKind.Sequential)]
    private struct WinPoint
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out WinPoint lpPoint);

    private const int WS_EX_TOPMOST = 0x00000008;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int X,
        int Y,
        int cx,
        int cy,
        uint uFlags);

    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_FRAMECHANGED = 0x0020;
    private const uint SWP_SHOWWINDOW = 0x0040;

    private const int WM_WINDOWPOSCHANGING = 0x0046;
    private const int WM_ACTIVATEAPP = 0x001C;
    private const int WM_ACTIVATE = 0x0006;

    [StructLayout(LayoutKind.Sequential)]
    private struct WINDOWPOS
    {
        public IntPtr hwnd;
        public IntPtr hwndInsertAfter;
        public int x;
        public int y;
        public int cx;
        public int cy;
        public uint flags;
    }
    #endregion

    private readonly MediaService _mediaService = new();
    private readonly SystemStatsService _statsService = new();
    private readonly WeatherService _weatherService = new();
    private readonly NotificationService _notificationService = new();
    private readonly ScreenOverlayService _screenOverlay = new();
    private readonly NativeAvatarRenderer _nativeAvatar = new();
    private readonly DispatcherTimer _avatarReactionTimer = new();

    private readonly DispatcherTimer _loopTimer = new();
    private readonly DispatcherTimer _ambientColorTimer = new();
    private IntPtr _lastForegroundHwnd = IntPtr.Zero;
    private Color _lastAppliedAmbientColor = Colors.Transparent;
    private DateTime _lastAmbientSampleTime = DateTime.MinValue;

    private readonly DispatcherTimer _hoverExpandTimer = new();
    private readonly DispatcherTimer _hoverCollapseTimer = new();
    private readonly DispatcherTimer _quickVolTimer = new();
    private readonly DispatcherTimer _toastTimer = new();
    private readonly DispatcherTimer _countdownTimer = new();
    private readonly DispatcherTimer _snipWatchTimer = new();
    private readonly DispatcherTimer _ghostCheckTimer = new();

    private Forms.NotifyIcon? _notifyIcon;

    private LyricsData? _currentLyrics;
    private string _lastLyricsTrackSignature = "";
    private int _lastActiveLyricIndex = -2;
    private IntPtr _lastTargetWindow = IntPtr.Zero;
    private readonly List<TextBlock> _renderedLyricTextBlocks = new();
    private AppSavedState _savedState = new();

    private bool _isExpanded;
    private bool _isPinned;
    private bool _isStealthBarMode;
    private bool _isAvatarMiniMode;
    private double _preMiniLeft;
    private double _preMiniTop;
    private EventHandler? _currentWindowMoveHandler;
    private bool _isSleepingForFullscreen;
    private bool _isDraggingIsland;
    private System.Windows.Point _dragStartScreenPoint;
    private double _dragStartWindowLeft;
    private int _activeTabIndex;
    private int _masterVolume = 50;
    private bool _isMuted;
    private bool _isDraggingSeekSlider;
    private bool _isAnimating;
    private int _loopTickCounter;
    private string _lastClipboardText = "";
    private BitmapImage? _lastAnalyzedCover;
    private bool? _lastChargingState;
    private int _snipWatchTicksRemaining;
    private readonly DispatcherTimer _danceTimer = new();
    private bool _danceBeatFlip;
    private bool _isMediaPlaying;
    private readonly DispatcherTimer _speechBubbleTimer = new();
    private readonly DispatcherTimer _miniHoldTimer = new();
    private bool _miniLongPressTriggered;
    private string _lastTrackSignature = "";
    private SystemStatsSnapshot? _lastSystemStats;
    private bool _hasAlertedLowBattery;
    private bool _hasAlertedHighLoad;

    // Pomodoro / Timer state
    private int _timerRemainingSeconds = 25 * 60;
    private int _timerTotalSeconds = 25 * 60;
    private bool _isTimerRunning;

    // Global Hotkeys
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private const int HOTKEY_ID_ALT_SPACE = 9001;
    private const int HOTKEY_ID_CTRL_SPACE = 9002;
    private const int HOTKEY_ID_CTRL_TILDE = 9003;
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_NOREPEAT = 0x4000;
    private const int WM_HOTKEY = 0x0312;
    private const uint VK_SPACE = 0x20;
    private const uint VK_OEM_3 = 0xC0;

    private HwndSource? _hwndSource;
    private DateTime _lastExitMiniModeTime = DateTime.MinValue;

    // Coucou Notch Companion & Interactive State
    private readonly DispatcherTimer _bezelVolTimer = new();
    private bool _handBeat;
    private int _pokeClickCount;
    private DateTime _lastPokeTime = DateTime.MinValue;
    private bool _isStealthRetracted;
    private DateTime _lastInteractionTime = DateTime.Now;

    public MainWindow()
    {
        InitializeComponent();
        InitializeNativeAvatar();

        _bezelVolTimer.Interval = TimeSpan.FromMilliseconds(1200);
        _bezelVolTimer.Tick += (s, e) =>
        {
            _bezelVolTimer.Stop();
            var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(200));
            fade.Completed += (_, _) => NotchBottomVolumeBar.Visibility = Visibility.Collapsed;
            NotchBottomVolumeBar.BeginAnimation(OpacityProperty, fade);
        };

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        try
        {
            var helper = new WindowInteropHelper(this);
            _hwndSource = HwndSource.FromHwnd(helper.Handle);
            _hwndSource?.AddHook(HwndHook);

            RegisterHotKey(helper.Handle, HOTKEY_ID_ALT_SPACE, MOD_ALT | MOD_NOREPEAT, VK_SPACE);
            RegisterHotKey(helper.Handle, HOTKEY_ID_CTRL_SPACE, MOD_CONTROL | MOD_NOREPEAT, VK_SPACE);
            RegisterHotKey(helper.Handle, HOTKEY_ID_CTRL_TILDE, MOD_CONTROL | MOD_NOREPEAT, VK_OEM_3);
        }
        catch
        {
        }
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {

        if (msg == WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            if (id is HOTKEY_ID_ALT_SPACE or HOTKEY_ID_CTRL_SPACE or HOTKEY_ID_CTRL_TILDE)
            {
                handled = true;
                ToggleIslandFromHotkey(id);
            }
        }
        else if (msg == WM_WINDOWPOSCHANGING && !_isSleepingForFullscreen)
        {
            try
            {
                WINDOWPOS pos = Marshal.PtrToStructure<WINDOWPOS>(lParam);
                pos.hwndInsertAfter = HWND_TOPMOST;
                pos.flags &= ~SWP_NOZORDER;
                Marshal.StructureToPtr(pos, lParam, false);
            }
            catch { }
        }
        else if ((msg == WM_ACTIVATEAPP || msg == WM_ACTIVATE) && !_isSleepingForFullscreen)
        {
            EnsureTopmost();
        }

        return IntPtr.Zero;
    }

    private void ToggleIslandFromHotkey(int hotkeyId)
    {
        if (hotkeyId == HOTKEY_ID_CTRL_TILDE)
        {
            if (_isExpanded)
            {
                if (SpotlightOverlay.Visibility == Visibility.Visible)
                {
                    CloseSpotlight();
                }
                CollapseIsland();
            }
            else
            {
                ExpandIsland(_activeTabIndex);
                Activate();
                Focus();
            }
            return;
        }

        // Alt+Space or Ctrl+Space: Spotlight Quick Toggle
        if (_isExpanded && SpotlightOverlay.Visibility == Visibility.Visible)
        {
            CloseSpotlight();
            CollapseIsland();
        }
        else
        {
            OpenSpotlight();
        }
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyWindowStyles();

        Deactivated += (s, e) => EnsureTopmost();
        Activated += (s, e) => EnsureTopmost();

        SpotlightService.InitializeIndex();

        _savedState = StorageService.Load();
        _isPinned = _savedState.IsPinned;
        _isStealthBarMode = _savedState.IsStealthBarMode;
        SoundEffectsService.IsEnabled = _savedState.SoundEffectsEnabled;
        SoundEffectsService.CurrentTheme = (NotificationSoundTheme)Math.Clamp(_savedState.NotificationSoundTheme, 0, 4);

        if (_savedState.CustomLeftX.HasValue)
        {
            double screenW = SystemParameters.PrimaryScreenWidth;
            Left = Math.Clamp(_savedState.CustomLeftX.Value, -100, screenW - Width + 100);
            Top = 0;
        }
        else
        {
            CenterAtTop();
        }

        BrightnessSlider.Value = Math.Clamp(_savedState.ScreenBrightness, 25, 100);
        BrightnessPercentText.Text = $"{(int)BrightnessSlider.Value}%";
        _screenOverlay.SetState((int)BrightnessSlider.Value, _savedState.WarmNightMode);
        _weatherService.ConfigureCustomLocation(_savedState.CustomWeatherCity, _savedState.CustomWeatherLat, _savedState.CustomWeatherLon);

        UpdatePinButtonVisual();
        UpdateStealthModeVisual();
        UpdateNightModeVisual();
        UpdateSettingsVisuals();
        var helper = new WindowInteropHelper(this);
        BackdropService.ClearWindowFrameAndBackdrop(helper.Handle);
        int startTab = Math.Clamp(_savedState.SelectedTab, 0, 4);

        if (_isStealthBarMode && !_isExpanded)
        {
            ApplyCollapsedDimensionsImmediate();
        }

        if (!string.IsNullOrWhiteSpace(_savedState.QuickNote))
        {
            QuickNoteTextBox.Text = _savedState.QuickNote;
        }

        RefreshFileTrayUi();
        RefreshClipboardUi();
        RefreshDockUi();
        UpdateTimerRingProgress(1.0);
        SwitchTab(startTab);
        InitSystemTray();

        // Setup timers
        _hoverExpandTimer.Interval = TimeSpan.FromMilliseconds(70);
        _hoverExpandTimer.Tick += (s, args) =>
        {
            _hoverExpandTimer.Stop();
            if (!_isSleepingForFullscreen && !_isDraggingIsland && !_isAvatarMiniMode)
            {
                ExpandIsland();
            }
        };

        _hoverCollapseTimer.Interval = TimeSpan.FromMilliseconds(700);
        _hoverCollapseTimer.Tick += (s, args) =>
        {
            _hoverCollapseTimer.Stop();
            if (!_isPinned && !IsMouseOver && !_isDraggingIsland)
            {
                CollapseIsland();
            }
        };

        _quickVolTimer.Interval = TimeSpan.FromMilliseconds(1200);
        _quickVolTimer.Tick += (s, args) =>
        {
            _quickVolTimer.Stop();
            QuickVolOverlay.Visibility = Visibility.Collapsed;
        };

        _toastTimer.Interval = TimeSpan.FromMilliseconds(3200);
        _toastTimer.Tick += (s, args) =>
        {
            _toastTimer.Stop();
            ToastBannerOverlay.Visibility = Visibility.Collapsed;
        };

        _countdownTimer.Interval = TimeSpan.FromSeconds(1);
        _countdownTimer.Tick += CountdownTimer_Tick;

        _snipWatchTimer.Interval = TimeSpan.FromMilliseconds(700);
        _snipWatchTimer.Tick += SnipWatchTimer_Tick;

        _ghostCheckTimer.Interval = TimeSpan.FromMilliseconds(2000);
        _ghostCheckTimer.Tick += GhostCheckTimer_Tick;
        _ghostCheckTimer.Start();

        _loopTimer.Interval = TimeSpan.FromMilliseconds(1000);
        _loopTimer.Tick += async (s, args) => await MainUpdateLoopAsync();
        _loopTimer.Start();

        _ambientColorTimer.Interval = TimeSpan.FromMilliseconds(1200);
        _ambientColorTimer.Tick += (s, args) => CheckAndUpdateAmbientColor();
        _ambientColorTimer.Start();

        _danceTimer.Interval = TimeSpan.FromMilliseconds(460);
        _danceTimer.Tick += (s, args) => DanceTimer_Tick();

        _speechBubbleTimer.Interval = TimeSpan.FromMilliseconds(2800);
        _speechBubbleTimer.Tick += (s, args) => HideMiniSpeechBubble();

        _miniHoldTimer.Interval = TimeSpan.FromMilliseconds(350);
        _miniHoldTimer.Tick += (s, args) =>
        {
            _miniHoldTimer.Stop();
            _miniLongPressTriggered = true;
            ShowMiniRadialControls();
        };

        SettingsService.Load();
        SettingsService.SettingsChanged += s => Dispatcher.InvokeAsync(() => ApplyAllSettings(s));
        ApplyAllSettings(SettingsService.Current);

        if (_savedState.IsInAvatarMiniMode && SettingsService.Current.MiniMode.MiniModeEnabled)
        {
            EnterAvatarMiniMode(immediate: true);
        }

        var initAmbient = SampleCurrentAmbientColorAroundIsland();
        _lastAppliedAmbientColor = initAmbient;
        ApplyAdaptiveAccentColor(initAmbient);

        await _mediaService.InitializeAsync();
        await _notificationService.InitializeAsync();
        UpdateVolumeFromSystem();
        await RefreshAudioDeviceLabelAsync();
        await MainUpdateLoopAsync();
    }

    private void ApplyWindowStyles()
    {
        var helper = new WindowInteropHelper(this);
        int exStyle = GetWindowLong(helper.Handle, GWL_EXSTYLE);
        SetWindowLong(helper.Handle, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW | WS_EX_TOPMOST);
        SetWindowPos(helper.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_FRAMECHANGED | SWP_SHOWWINDOW);
    }

    private void SetFullscreenSleepMode(bool sleep)
    {
        if (_isSleepingForFullscreen == sleep) return;
        _isSleepingForFullscreen = sleep;

        var helper = new WindowInteropHelper(this);
        if (helper.Handle == IntPtr.Zero) return;

        int exStyle = GetWindowLong(helper.Handle, GWL_EXSTYLE);
        if (sleep)
        {
            _hoverExpandTimer.Stop();
            SetWindowLong(helper.Handle, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW | WS_EX_TRANSPARENT | WS_EX_TOPMOST);
            if (_isExpanded)
            {
                CollapseIsland();
            }
            IslandContainer.IsHitTestVisible = false;
            IslandContainer.Opacity = 0.0;
            HideMiniSpeechBubble();
            HideMiniRadialControls();
            Visibility = Visibility.Hidden;
        }
        else
        {
            Visibility = Visibility.Visible;
            IslandContainer.IsHitTestVisible = true;
            SetWindowLong(helper.Handle, GWL_EXSTYLE, (exStyle | WS_EX_TOOLWINDOW | WS_EX_TOPMOST) & ~WS_EX_TRANSPARENT);
            SetWindowPos(helper.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_FRAMECHANGED | SWP_SHOWWINDOW);
            IslandContainer.Opacity = 1.0;
        }
    }

    private void SetClickThroughGhostMode(bool enable)
    {
        // Replaced by Avatar Mini Mode
    }

    public void EnsureTopmost()
    {
        if (_isSleepingForFullscreen) return;
        try
        {
            var helper = new WindowInteropHelper(this);
            IntPtr hwnd = helper.Handle;
            if (hwnd == IntPtr.Zero) return;
            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }
        catch { }
    }

    private void GhostCheckTimer_Tick(object? sender, EventArgs e)
    {
        string triggerFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test_trigger.txt");
        if (File.Exists(triggerFile))
        {
            try
            {
                string cmd = File.ReadAllText(triggerFile).Trim();
                File.Delete(triggerFile);
                if (cmd == "mini") EnterAvatarMiniMode();
                else if (cmd == "normal") ExitAvatarMiniMode();
                else if (cmd == "toggle")
                {
                    if (_isAvatarMiniMode) ExitAvatarMiniMode(); else EnterAvatarMiniMode();
                }
                else if (cmd == "wake")
                {
                    WakeUpAndShowIsland();
                }
                else if (cmd == "settings")
                {
                    OpenSettingsWindow();
                }
                else if (cmd == "expand")
                {
                    _isPinned = true;
                    UpdatePinButtonVisual();
                    ExpandIsland(0);
                }
                else if (cmd == "collapse")
                {
                    _isPinned = false;
                    UpdatePinButtonVisual();
                    CollapseIsland();
                }
                else if (cmd == "pet")
                {
                    TriggerPokePetEffect();
                }
                else if (cmd == "pomo")
                {
                    ShowToastBanner("🎯", "Odaklanma Başladı", "Odak modu aktif.");
                }
                else if (cmd == "pomowin")
                {
                    ShowToastBanner("🏆", "Pomodoro Tamamlandı!", "Tebrikler! +50 Odak XP kazandın.");
                }
                else if (cmd == "vol")
                {
                    ShowBezelVolume(75);
                }
                else if (cmd == "mockmedia")
                {
                    _isMediaActive = true;
                    CollapsedCenterGrid.Visibility = Visibility.Collapsed;
                    MiniMediaInfoPanel.Visibility = Visibility.Visible;
                    MiniTrackTitleText.Text = "Blinding Lights";
                    MiniTrackArtistText.Text = "The Weeknd";
                    MiniTrackArtistText.Visibility = Visibility.Visible;
                    MiniThumbBorder.Visibility = Visibility.Visible;
                    UpdatePlayPauseVectorIcon(true);
                }
                else if (cmd == "expandmedia")
                {
                    _isMediaActive = true;
                    CollapsedCenterGrid.Visibility = Visibility.Collapsed;
                    MiniMediaInfoPanel.Visibility = Visibility.Visible;
                    ExpandedTrackTitle.Text = "Blinding Lights";
                    ExpandedArtist.Text = "The Weeknd";
                    MediaAppText.Text = "Spotify";
                    TrackSeekSlider.IsEnabled = true;
                    TrackSeekSlider.Maximum = 200;
                    TrackSeekSlider.Value = 68;
                    CurrentTimeText.Text = "01:08";
                    TotalTimeText.Text = "03:20";
                    UpdatePlayPauseVectorIcon(true);
                    _isPinned = true;
                    UpdatePinButtonVisual();
                    ExpandIsland(0);
                }
                else if (cmd == "snap")
                {
                    ToastBannerOverlay.Visibility = Visibility.Collapsed;
                    _toastTimer.Stop();
                    UpdateLayout();
                    int w = (int)Math.Max(1, ActualWidth > 0 ? ActualWidth : Width);
                    int h = (int)Math.Max(1, ActualHeight > 0 ? ActualHeight : Height);
                    var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(w, h, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                    rtb.Render(this);
                    var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
                    enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
                    using var fs = File.Create(@"C:\Users\ali\.gemini\antigravity\scratch\DynamicIslandWin\snap_island.png");
                    enc.Save(fs);
                }
            }
            catch { }
        }

        var helper = new WindowInteropHelper(this);
        var myHwnd = helper.Handle;
        if (myHwnd != IntPtr.Zero)
        {
            bool shouldSleep = _savedState.AutoHideFullscreen && SystemStatsService.IsFullscreenAppActive(myHwnd);
            SetFullscreenSleepMode(shouldSleep);
        }

        if (_isSleepingForFullscreen) return;

        EnsureTopmost();

        if (GetCursorPos(out WinPoint pt))
        {
            UpdateCursorTracking(pt);
            UpdateStealthPeekLogic(pt);
        }

        try
        {
            IntPtr fg = GetForegroundWindow();
            if (fg != IntPtr.Zero && fg != myHwnd)
            {
                _lastTargetWindow = fg;
            }
        }
        catch
        {
        }
    }

    #region Coucou Companion Mechanics (Eye Tracking, Floating Hands, Petting, Stealth & Bezel Volume)
    private void UpdateCursorTracking(WinPoint pt)
    {
        try
        {
            if (_isAvatarMiniMode)
            {
                // Mini mode in top-right
                double avatarX = Left + ((ActualWidth > 0 ? ActualWidth : Width) / 2.0);
                double avatarY = Top + 12.0 + 29.0;
                double dx = pt.X - avatarX;
                double dy = pt.Y - avatarY;
                double tx = Math.Clamp(dx * 0.007, -3.2, 3.2);
                double ty = Math.Clamp(dy * 0.005, -2.4, 2.4);

                if (LeftEyeTrackTranslate != null) LeftEyeTrackTranslate.X += (tx - LeftEyeTrackTranslate.X) * 0.35;
                if (LeftEyeTrackTranslate != null) LeftEyeTrackTranslate.Y += (ty - LeftEyeTrackTranslate.Y) * 0.35;
                if (RightEyeTrackTranslate != null) RightEyeTrackTranslate.X += (tx - RightEyeTrackTranslate.X) * 0.35;
                if (RightEyeTrackTranslate != null) RightEyeTrackTranslate.Y += (ty - RightEyeTrackTranslate.Y) * 0.35;
            }
            else
            {
                // Normal Notch mode (center top)
                double avatarX = Left + ((ActualWidth > 0 ? ActualWidth : Width) / 2.0);
                double avatarY = Top + 20.0;
                double dx = pt.X - avatarX;
                double dy = pt.Y - avatarY;
                double tx = Math.Clamp(dx * 0.007, -3.2, 3.2);
                double ty = Math.Clamp(dy * 0.005, -2.4, 2.4);

                if (NormalIslandLeftEyeTrack != null) NormalIslandLeftEyeTrack.X += (tx - NormalIslandLeftEyeTrack.X) * 0.35;
                if (NormalIslandLeftEyeTrack != null) NormalIslandLeftEyeTrack.Y += (ty - NormalIslandLeftEyeTrack.Y) * 0.35;
                if (NormalIslandRightEyeTrack != null) NormalIslandRightEyeTrack.X += (tx - NormalIslandRightEyeTrack.X) * 0.35;
                if (NormalIslandRightEyeTrack != null) NormalIslandRightEyeTrack.Y += (ty - NormalIslandRightEyeTrack.Y) * 0.35;

                // Expanded Stage Avatar (on the right of the media player)
                if (_isExpanded && TabMediaView != null && TabMediaView.Visibility == Visibility.Visible)
                {
                    double stageCenterX = Left + ((ActualWidth > 0 ? ActualWidth : Width) / 2.0) + 260.0;
                    double stageCenterY = Top + 105.0;
                    double edx = pt.X - stageCenterX;
                    double edy = pt.Y - stageCenterY;
                    double etx = Math.Clamp(edx * 0.007, -4.5, 4.5);
                    double ety = Math.Clamp(edy * 0.005, -3.5, 3.5);

                    if (ExpandedLeftEyeTrack != null) ExpandedLeftEyeTrack.X += (etx - ExpandedLeftEyeTrack.X) * 0.35;
                    if (ExpandedLeftEyeTrack != null) ExpandedLeftEyeTrack.Y += (ety - ExpandedLeftEyeTrack.Y) * 0.35;
                    if (ExpandedRightEyeTrack != null) ExpandedRightEyeTrack.X += (etx - ExpandedRightEyeTrack.X) * 0.35;
                    if (ExpandedRightEyeTrack != null) ExpandedRightEyeTrack.Y += (ety - ExpandedRightEyeTrack.Y) * 0.35;
                }
            }
        }
        catch { }
    }

    private void UpdateStealthPeekLogic(WinPoint pt)
    {
        // STRICT CONSTRAINT: Only operate in stealth bar mode; never interfere in normal floating island or avatar mode
        if (!_isStealthBarMode || _isAvatarMiniMode || _isExpanded || _isPinned || _isAnimating) return;

        double notchCenter = Left + ((ActualWidth > 0 ? ActualWidth : Width) / 2.0);
        double halfW = IslandContainer.ActualWidth / 2.0 + 40.0;
        bool isNearNotch = pt.Y <= 50 && Math.Abs(pt.X - notchCenter) <= halfW;

        if (isNearNotch || (_isMediaActive && _isMediaPlaying))
        {
            _lastInteractionTime = DateTime.Now;
            if (_isStealthRetracted)
            {
                ExitStealthNotch();
            }
            return;
        }

        // Idle timeout: 70 seconds
        if (!_isStealthRetracted && (DateTime.Now - _lastInteractionTime).TotalSeconds > 70)
        {
            EnterStealthNotch();
        }
    }

    private void EnterStealthNotch()
    {
        if (_isStealthRetracted || _isAvatarMiniMode || _isExpanded) return;
        _isStealthRetracted = true;

        var hAnim = new DoubleAnimation(6, TimeSpan.FromMilliseconds(280)) { EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseInOut } };
        Timeline.SetDesiredFrameRate(hAnim, 144);
        IslandContainer.BeginAnimation(HeightProperty, hAnim);

        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(160));
        Timeline.SetDesiredFrameRate(fade, 144);
        CollapsedView.BeginAnimation(OpacityProperty, fade);

        // Tuck hands
        OneeLeftHandTranslate?.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(15, TimeSpan.FromMilliseconds(200)));
        OneeRightHandTranslate?.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(15, TimeSpan.FromMilliseconds(200)));
    }

    private void ExitStealthNotch()
    {
        if (!_isStealthRetracted) return;
        _isStealthRetracted = false;

        var hAnim = new DoubleAnimation(42, TimeSpan.FromMilliseconds(320)) { EasingFunction = new BackEase { Amplitude = 0.25, EasingMode = EasingMode.EaseOut } };
        Timeline.SetDesiredFrameRate(hAnim, 144);
        IslandContainer.BeginAnimation(HeightProperty, hAnim);

        var fade = new DoubleAnimation(1, TimeSpan.FromMilliseconds(240)) { BeginTime = TimeSpan.FromMilliseconds(80) };
        Timeline.SetDesiredFrameRate(fade, 144);
        CollapsedView.BeginAnimation(OpacityProperty, fade);

        // Onee peeks down hands
        OneeLeftHandTranslate?.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(220)));
        OneeRightHandTranslate?.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(220)));
    }

    public void ShowBezelVolume(int volume)
    {
        try
        {
            NotchBottomVolumeBar.Visibility = Visibility.Visible;
            NotchBottomVolumeBar.BeginAnimation(OpacityProperty, null);
            NotchBottomVolumeBar.Opacity = 1.0;

            double maxW = Math.Max(20, IslandContainer.ActualWidth - 52);
            double targetW = Math.Clamp(maxW * (volume / 100.0), 6, maxW);
            NotchBottomVolumeFill.Width = targetW;

            _bezelVolTimer.Stop();
            _bezelVolTimer.Start();
        }
        catch { }
    }

    private void MiniAvatar_MouseEnter(object sender, MouseEventArgs e)
    {
        OneeLeftHandTranslate?.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-5, TimeSpan.FromMilliseconds(180)));
        OneeRightHandTranslate?.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-5, TimeSpan.FromMilliseconds(180)));
    }

    private void MiniAvatar_MouseLeave(object sender, MouseEventArgs e)
    {
        OneeLeftHandTranslate?.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(200)));
        OneeRightHandTranslate?.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(200)));
    }

    private void ExpandedAvatar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        TriggerPokePetEffect();
        e.Handled = true;
    }

    private void TriggerPokePetEffect()
    {
        var now = DateTime.Now;
        if ((now - _lastPokeTime).TotalMilliseconds < 800)
        {
            _pokeClickCount++;
        }
        else
        {
            _pokeClickCount = 1;
        }
        _lastPokeTime = now;

        if (_pokeClickCount >= 3)
        {
            _pokeClickCount = 0;
            _nativeAvatar.Play("drowsy");
            AvatarCurrentAnimLabel.Text = " • @_@ başı döndü!";

            var shake = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(450) };
            shake.KeyFrames.Add(new LinearDoubleKeyFrame(-4, KeyTime.FromPercent(0.2)));
            shake.KeyFrames.Add(new LinearDoubleKeyFrame(4, KeyTime.FromPercent(0.4)));
            shake.KeyFrames.Add(new LinearDoubleKeyFrame(-3, KeyTime.FromPercent(0.6)));
            shake.KeyFrames.Add(new LinearDoubleKeyFrame(2, KeyTime.FromPercent(0.8)));
            shake.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(1.0)));
            NormalIslandAvatarBobTranslate.BeginAnimation(TranslateTransform.XProperty, shake);
            ExpandedAvatarBobTranslate?.BeginAnimation(TranslateTransform.XProperty, shake);
            return;
        }

        TriggerAvatarReaction(AvatarState.Happy, 2);

        PetHeartParticle.BeginAnimation(OpacityProperty, null);
        PetHeartTranslate.BeginAnimation(TranslateTransform.YProperty, null);
        PetHeartParticle.Opacity = 1.0;
        PetHeartTranslate.Y = 0;

        var heartMove = new DoubleAnimation(0, -32, TimeSpan.FromMilliseconds(700)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        var heartFade = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(700));
        PetHeartTranslate.BeginAnimation(TranslateTransform.YProperty, heartMove);
        PetHeartParticle.BeginAnimation(OpacityProperty, heartFade);

        if (ExpandedStageHeartParticle != null && ExpandedStageHeartTranslate != null)
        {
            ExpandedStageHeartParticle.BeginAnimation(OpacityProperty, null);
            ExpandedStageHeartTranslate.BeginAnimation(TranslateTransform.YProperty, null);
            ExpandedStageHeartParticle.Opacity = 1.0;
            ExpandedStageHeartTranslate.Y = 0;
            ExpandedStageHeartTranslate.BeginAnimation(TranslateTransform.YProperty, heartMove);
            ExpandedStageHeartParticle.BeginAnimation(OpacityProperty, heartFade);
        }
    }

    private void IslandContainer_DragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;

            // Coucou Paws Reach Up for Snack
            var pawReach = new DoubleAnimation(-16, TimeSpan.FromMilliseconds(160)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            OneeLeftHandTranslate?.BeginAnimation(TranslateTransform.YProperty, pawReach);
            OneeRightHandTranslate?.BeginAnimation(TranslateTransform.YProperty, pawReach);
            MiniBubbleLeftHandTranslate?.BeginAnimation(TranslateTransform.YProperty, pawReach);
            MiniBubbleRightHandTranslate?.BeginAnimation(TranslateTransform.YProperty, pawReach);
            ExpandedStageLeftHandTranslate?.BeginAnimation(TranslateTransform.YProperty, pawReach);
            ExpandedStageRightHandTranslate?.BeginAnimation(TranslateTransform.YProperty, pawReach);

            var mouthOpen = new DoubleAnimation(1.35, TimeSpan.FromMilliseconds(160)) { EasingFunction = new BackEase { Amplitude = 0.3, EasingMode = EasingMode.EaseOut } };
            OneeNormalMouthScale?.BeginAnimation(ScaleTransform.ScaleXProperty, mouthOpen);
            OneeNormalMouthScale?.BeginAnimation(ScaleTransform.ScaleYProperty, mouthOpen);
            MiniBubbleMouthScale?.BeginAnimation(ScaleTransform.ScaleXProperty, mouthOpen);
            MiniBubbleMouthScale?.BeginAnimation(ScaleTransform.ScaleYProperty, mouthOpen);
            ExpandedStageMouthScale?.BeginAnimation(ScaleTransform.ScaleXProperty, mouthOpen);
            ExpandedStageMouthScale?.BeginAnimation(ScaleTransform.ScaleYProperty, mouthOpen);

            _nativeAvatar.Play("curious");
        }
    }

    private void IslandContainer_DragLeave(object sender, DragEventArgs e)
    {
        var pawDown = new DoubleAnimation(0, TimeSpan.FromMilliseconds(180));
        OneeLeftHandTranslate?.BeginAnimation(TranslateTransform.YProperty, pawDown);
        OneeRightHandTranslate?.BeginAnimation(TranslateTransform.YProperty, pawDown);
        MiniBubbleLeftHandTranslate?.BeginAnimation(TranslateTransform.YProperty, pawDown);
        MiniBubbleRightHandTranslate?.BeginAnimation(TranslateTransform.YProperty, pawDown);
        ExpandedStageLeftHandTranslate?.BeginAnimation(TranslateTransform.YProperty, pawDown);
        ExpandedStageRightHandTranslate?.BeginAnimation(TranslateTransform.YProperty, pawDown);

        var mouthClose = new DoubleAnimation(0, TimeSpan.FromMilliseconds(150));
        OneeNormalMouthScale?.BeginAnimation(ScaleTransform.ScaleXProperty, mouthClose);
        OneeNormalMouthScale?.BeginAnimation(ScaleTransform.ScaleYProperty, mouthClose);
        MiniBubbleMouthScale?.BeginAnimation(ScaleTransform.ScaleXProperty, mouthClose);
        MiniBubbleMouthScale?.BeginAnimation(ScaleTransform.ScaleYProperty, mouthClose);
        ExpandedStageMouthScale?.BeginAnimation(ScaleTransform.ScaleXProperty, mouthClose);
        ExpandedStageMouthScale?.BeginAnimation(ScaleTransform.ScaleYProperty, mouthClose);

        RestoreDefaultAvatarState();
    }

    private void IslandContainer_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
        {
            // Coucou Chewing / Swallowing KeyFrame Animation (nom nom nom -> swallow!)
            var chewAnim = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(650) };
            chewAnim.KeyFrames.Add(new LinearDoubleKeyFrame(0.15, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(90))));
            chewAnim.KeyFrames.Add(new LinearDoubleKeyFrame(1.15, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(220))));
            chewAnim.KeyFrames.Add(new LinearDoubleKeyFrame(0.15, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(340))));
            chewAnim.KeyFrames.Add(new LinearDoubleKeyFrame(0.85, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(460))));
            chewAnim.KeyFrames.Add(new LinearDoubleKeyFrame(0.00, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(620))));

            OneeNormalMouthScale?.BeginAnimation(ScaleTransform.ScaleYProperty, chewAnim);
            OneeNormalMouthScale?.BeginAnimation(ScaleTransform.ScaleXProperty, chewAnim);
            MiniBubbleMouthScale?.BeginAnimation(ScaleTransform.ScaleYProperty, chewAnim);
            MiniBubbleMouthScale?.BeginAnimation(ScaleTransform.ScaleXProperty, chewAnim);
            ExpandedStageMouthScale?.BeginAnimation(ScaleTransform.ScaleYProperty, chewAnim);
            ExpandedStageMouthScale?.BeginAnimation(ScaleTransform.ScaleXProperty, chewAnim);

            // Happy Clapping Paws
            var pawChew = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(550) };
            pawChew.KeyFrames.Add(new LinearDoubleKeyFrame(6, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(100))));
            pawChew.KeyFrames.Add(new LinearDoubleKeyFrame(-6, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(260))));
            pawChew.KeyFrames.Add(new LinearDoubleKeyFrame(3, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(400))));
            pawChew.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(550))));

            OneeLeftHandTranslate?.BeginAnimation(TranslateTransform.YProperty, pawChew);
            OneeRightHandTranslate?.BeginAnimation(TranslateTransform.YProperty, pawChew);
            MiniBubbleLeftHandTranslate?.BeginAnimation(TranslateTransform.YProperty, pawChew);
            MiniBubbleRightHandTranslate?.BeginAnimation(TranslateTransform.YProperty, pawChew);
            ExpandedStageLeftHandTranslate?.BeginAnimation(TranslateTransform.YProperty, pawChew);
            ExpandedStageRightHandTranslate?.BeginAnimation(TranslateTransform.YProperty, pawChew);

            int addedCount = 0;
            foreach (var f in files)
            {
                var item = StorageService.CreateTrayItem(f);
                if (item != null)
                {
                    _savedState.TrayFiles.RemoveAll(x => x.FullPath.Equals(item.FullPath, StringComparison.OrdinalIgnoreCase));
                    _savedState.TrayFiles.Insert(0, item);
                    addedCount++;
                }
            }

            if (addedCount > 0)
            {
                StorageService.Save(_savedState);
                RefreshFileTrayUi();
                TriggerAvatarReaction(AvatarState.Happy, 3);
                TriggerPokePetEffect();
                ShowToastBanner("😋", "Ham! Dosya Yutuldu", $"{addedCount} dosya Onee tarafından tepsiye alındı.");
            }
        }
        else
        {
            IslandContainer_DragLeave(sender, e);
        }
    }

    private void TriggerLiquidColorWave(Color accent)
    {
        try
        {
            if (_isAvatarMiniMode || LiquidColorWave == null || LiquidWaveTranslate == null || WaveStopCore == null) return;

            Color brightShimmer = Color.FromArgb(
                190,
                (byte)Math.Min(255, accent.R + 60),
                (byte)Math.Min(255, accent.G + 60),
                (byte)Math.Min(255, accent.B + 60));
            WaveStopCore.Color = brightShimmer;
            LiquidColorWave.Opacity = 1.0;

            double islandW = Math.Max(356, IslandContainer.ActualWidth);
            LiquidWaveTranslate.BeginAnimation(TranslateTransform.XProperty, null);
            LiquidColorWave.BeginAnimation(OpacityProperty, null);

            var moveAnim = new DoubleAnimation(-160, islandW + 40, TimeSpan.FromMilliseconds(650))
            {
                EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
            };
            var fadeAnim = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(650))
            {
                BeginTime = TimeSpan.FromMilliseconds(180)
            };
            Timeline.SetDesiredFrameRate(moveAnim, 144);
            Timeline.SetDesiredFrameRate(fadeAnim, 144);

            fadeAnim.Completed += (_, _) =>
            {
                LiquidColorWave.Opacity = 0.0;
            };

            LiquidWaveTranslate.BeginAnimation(TranslateTransform.XProperty, moveAnim);
            LiquidColorWave.BeginAnimation(OpacityProperty, fadeAnim);
        }
        catch { }
    }

    private bool _isRainAnimationRunning;
    private void UpdateAtmosphericWeather(WeatherSnapshot weather)
    {
        try
        {
            bool isRaining = weather.IsLoaded && (
                weather.ConditionText.Contains("yağmur", StringComparison.OrdinalIgnoreCase) ||
                weather.ConditionText.Contains("rain", StringComparison.OrdinalIgnoreCase) ||
                weather.ConditionText.Contains("sağanak", StringComparison.OrdinalIgnoreCase) ||
                weather.ConditionText.Contains("drizzle", StringComparison.OrdinalIgnoreCase) ||
                weather.Emoji.Contains("🌧") || weather.Emoji.Contains("🌦") || weather.Emoji.Contains("⛈"));

            if (isRaining && !_isAvatarMiniMode && !_isExpanded && NotchRainCanvas != null)
            {
                NotchRainCanvas.Visibility = Visibility.Visible;
                StartRainDropsAnimation();
            }
            else
            {
                if (NotchRainCanvas != null) NotchRainCanvas.Visibility = Visibility.Collapsed;
                StopRainDropsAnimation();
            }
        }
        catch { }
    }

    private void StartRainDropsAnimation()
    {
        if (_isRainAnimationRunning || RainDropLeftTranslate == null || RainDropRightTranslate == null) return;
        _isRainAnimationRunning = true;

        var dropFall1 = new DoubleAnimation(0, 26, TimeSpan.FromMilliseconds(1200))
        {
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseIn }
        };
        var dropFade1 = new DoubleAnimationUsingKeyFrames
        {
            RepeatBehavior = RepeatBehavior.Forever,
            Duration = TimeSpan.FromMilliseconds(1200)
        };
        dropFade1.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromPercent(0.0)));
        dropFade1.KeyFrames.Add(new LinearDoubleKeyFrame(0.85, KeyTime.FromPercent(0.25)));
        dropFade1.KeyFrames.Add(new LinearDoubleKeyFrame(0.85, KeyTime.FromPercent(0.75)));
        dropFade1.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromPercent(1.0)));

        var dropFall2 = new DoubleAnimation(0, 26, TimeSpan.FromMilliseconds(1500))
        {
            BeginTime = TimeSpan.FromMilliseconds(450),
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseIn }
        };
        var dropFade2 = new DoubleAnimationUsingKeyFrames
        {
            BeginTime = TimeSpan.FromMilliseconds(450),
            RepeatBehavior = RepeatBehavior.Forever,
            Duration = TimeSpan.FromMilliseconds(1500)
        };
        dropFade2.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromPercent(0.0)));
        dropFade2.KeyFrames.Add(new LinearDoubleKeyFrame(0.85, KeyTime.FromPercent(0.25)));
        dropFade2.KeyFrames.Add(new LinearDoubleKeyFrame(0.85, KeyTime.FromPercent(0.75)));
        dropFade2.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromPercent(1.0)));

        RainDropLeftTranslate.BeginAnimation(TranslateTransform.YProperty, dropFall1);
        RainDropLeft.BeginAnimation(OpacityProperty, dropFade1);

        RainDropRightTranslate.BeginAnimation(TranslateTransform.YProperty, dropFall2);
        RainDropRight.BeginAnimation(OpacityProperty, dropFade2);
    }

    private void StopRainDropsAnimation()
    {
        _isRainAnimationRunning = false;
        RainDropLeftTranslate?.BeginAnimation(TranslateTransform.YProperty, null);
        RainDropLeft?.BeginAnimation(OpacityProperty, null);
        RainDropRightTranslate?.BeginAnimation(TranslateTransform.YProperty, null);
        RainDropRight?.BeginAnimation(OpacityProperty, null);
    }
    #endregion

    public void WakeUpAndShowIsland()
    {
        Dispatcher.Invoke(() =>
        {
            if (_isAvatarMiniMode)
            {
                ExitAvatarMiniMode();
            }

            SetFullscreenSleepMode(false);
            Visibility = Visibility.Visible;
            IslandContainer.Opacity = 1.0;
            IslandContainer.IsHitTestVisible = true;
            EnsureTopmost();
            Activate();
            Focus();

            if (!_isExpanded)
            {
                ExpandIsland(_activeTabIndex);
            }

            SoundEffectsService.PlayExpand();
            TriggerAvatarReaction(AvatarState.Happy, 3);
        });
    }

    private void CenterAtTop()
    {
        double screenW = SystemParameters.PrimaryScreenWidth;
        Left = (screenW - Width) / 2.0;
        Top = 0;
    }

    private void InitSystemTray()
    {
        try
        {
            _notifyIcon = new Forms.NotifyIcon
            {
                Icon = System.Drawing.SystemIcons.Application,
                Text = "Dynamic Island Windows",
                Visible = true
            };

            var contextMenu = new Forms.ContextMenuStrip();

            var toggleItem = new Forms.ToolStripMenuItem("Göster / Gizle");
            toggleItem.Click += (s, e) =>
            {
                if (Visibility == Visibility.Visible) Hide();
                else { Show(); Activate(); }
            };

            var pinItem = new Forms.ToolStripMenuItem("Sabitlemeyi Aç / Kapat");
            pinItem.Click += (s, e) =>
            {
                _isPinned = !_isPinned;
                _savedState.IsPinned = _isPinned;
                StorageService.Save(_savedState);
                UpdatePinButtonVisual();
                if (_isPinned) ExpandIsland();
            };

            var snapItem = new Forms.ToolStripMenuItem("📸 Tam Ekran Görüntüsü Al");
            snapItem.Click += async (s, e) => await TakeFullScreenshotAsync();

            var resetPosItem = new Forms.ToolStripMenuItem("Konumu Ortala");
            resetPosItem.Click += (s, e) => CenterAtTop();

            var settingsItem = new Forms.ToolStripMenuItem("⚙️ Ayarlar (Settings)");
            settingsItem.Click += (s, e) => OpenSettingsWindow();

            var coffeeItem = new Forms.ToolStripMenuItem("☕ Geliştiriciye Kahve Ismarla (Buy Me a Coffee)");
            coffeeItem.Click += (s, e) =>
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://www.buymeacoffee.com/NovaCarpeDiem") { UseShellExecute = true });
                }
                catch { }
            };

            var exitItem = new Forms.ToolStripMenuItem("Çıkış");
            exitItem.Click += (s, e) =>
            {
                _notifyIcon.Visible = false;
                Close();
            };

            contextMenu.Items.Add(toggleItem);
            contextMenu.Items.Add(pinItem);
            contextMenu.Items.Add(snapItem);
            contextMenu.Items.Add(resetPosItem);
            contextMenu.Items.Add(new Forms.ToolStripSeparator());
            contextMenu.Items.Add(settingsItem);
            contextMenu.Items.Add(coffeeItem);
            contextMenu.Items.Add(new Forms.ToolStripSeparator());
            contextMenu.Items.Add(exitItem);

            _notifyIcon.ContextMenuStrip = contextMenu;
            _notifyIcon.DoubleClick += (s, e) =>
            {
                if (_isExpanded) CollapseIsland();
                else ExpandIsland();
            };
        }
        catch
        {
        }
    }

    #region Main Update Loop
    private async Task MainUpdateLoopAsync()
    {
        // Never block the UI thread with COM/WinRT polling while 144Hz expand/collapse animation is active
        if (_isAnimating) return;

        // 1. Smart Fullscreen Game / Movie Detection (Auto-Sleep)
        var hwnd = new WindowInteropHelper(this).Handle;
        bool shouldSleepForFullscreen = _savedState.AutoHideFullscreen && SystemStatsService.IsFullscreenAppActive(hwnd);
        SetFullscreenSleepMode(shouldSleepForFullscreen);
        if (_isSleepingForFullscreen) return;

        _loopTickCounter++;
        var now = DateTime.Now;
        string timeStr = now.ToString("HH:mm");
        string dayStr = now.ToString("dddd");

        var media = await _mediaService.GetCurrentTrackAsync();
        if (_isAnimating) return;
        UpdateMediaUi(media, timeStr, dayStr);

        // Poll network speed, system stats, volume, mic, weather, notifications & clipboard
        UpdateVolumeFromSystem();

        var stats = await _statsService.GetSnapshotAsync();
        if (_isAnimating) return;
        UpdateStatsUi(stats);

        if (_loopTickCounter % 3 == 1)
        {
            var weather = await _weatherService.GetWeatherAsync();
            UpdateWeatherUi(weather);

            var toast = await _notificationService.PollLatestWindowsNotificationAsync();
            if (toast != null)
            {
                if (_isAvatarMiniMode)
                {
                    TriggerAvatarReaction(AvatarState.Surprised, 3);
                    SoundEffectsService.PlayChime();
                }
                else
                {
                    ShowToastBanner(toast.Icon, toast.Title, toast.Message);
                }
            }

            CheckClipboardChanges();
        }
    }

    private void UpdateVolumeFromSystem()
    {
        var (vol, muted) = AudioService.GetVolumeState();
        _masterVolume = vol;
        _isMuted = muted;

        MiniVolText.Text = muted ? "🔇 Sessiz" : $"🔊 {_masterVolume}%";
        if (MasterVolumeSlider != null) MasterVolumeSlider.Value = _masterVolume;
        if (MiniVolumeSlider != null) MiniVolumeSlider.Value = _masterVolume;
        if (MasterVolumePercentText != null) MasterVolumePercentText.Text = $"{_masterVolume}%";
        if (MuteIconText != null) MuteIconText.Text = muted ? "🔇" : "🔊";

        var (hasMic, micMuted) = AudioService.GetMicMuteState();
        UpdateMicUi(hasMic ? micMuted : null);
    }

    private bool _isMediaActive;

    private Color SampleCurrentAmbientColorAroundIsland()
    {
        try
        {
            var dpi = VisualTreeHelper.GetDpi(this);
            double dpiX = dpi.DpiScaleX;
            double dpiY = dpi.DpiScaleY;

            var screenPt = IslandContainer.PointToScreen(new System.Windows.Point(0, 0));
            double islandL = screenPt.X;
            double islandT = screenPt.Y;
            double islandW = (IslandContainer.ActualWidth > 0 ? IslandContainer.ActualWidth : 356) * dpiX;
            double islandH = (IslandContainer.ActualHeight > 0 ? IslandContainer.ActualHeight : 42) * dpiY;

            var screen = Forms.Screen.FromPoint(new System.Drawing.Point((int)islandL, (int)islandT)) ?? Forms.Screen.PrimaryScreen;
            double screenW = screen?.Bounds.Width ?? (SystemParameters.PrimaryScreenWidth * dpiX);
            double screenH = screen?.Bounds.Height ?? (SystemParameters.PrimaryScreenHeight * dpiY);

            return AdaptiveColorService.ExtractDominantColorAroundIsland(
                islandL, islandT, islandW, islandH, screenW, screenH);
        }
        catch
        {
            return AdaptiveColorService.GetDesktopWallpaperDominantColor();
        }
    }

    private void CheckAndUpdateAmbientColor()
    {
        // When media is actively playing with an album cover, keep album art accent in both normal and mini mode
        if (_isMediaActive && _isMediaPlaying && _lastAnalyzedCover != null)
        {
            return;
        }

        if (_isAnimating)
        {
            return;
        }

        IntPtr fg = GetForegroundWindow();
        if (fg == _lastForegroundHwnd && _lastAppliedAmbientColor != Colors.Transparent && (DateTime.UtcNow - _lastAmbientSampleTime).TotalSeconds < 4.0)
        {
            return;
        }
        _lastForegroundHwnd = fg;
        _lastAmbientSampleTime = DateTime.UtcNow;

        var ambientColor = SampleCurrentAmbientColorAroundIsland();

        if (_lastAppliedAmbientColor == Colors.Transparent ||
            Math.Abs(ambientColor.R - _lastAppliedAmbientColor.R) > 8 ||
            Math.Abs(ambientColor.G - _lastAppliedAmbientColor.G) > 8 ||
            Math.Abs(ambientColor.B - _lastAppliedAmbientColor.B) > 8)
        {
            _lastAppliedAmbientColor = ambientColor;
            ApplyAdaptiveAccentColor(ambientColor);
        }
    }

    private void UpdateMicUi(bool? micMuted)
    {
        // Microphone badge removed per user request so Song Title + Artist fit cleanly
        MiniMicBorder.Visibility = Visibility.Collapsed;
        MicMuteBtn.Visibility = Visibility.Collapsed;
    }

    private void UpdateMediaUi(MediaTrackInfo media, string timeStr, string dayStr)
    {
        bool hasTrack = media.HasActiveSession && !string.IsNullOrWhiteSpace(media.Title);
        _isMediaActive = hasTrack;
        _isMediaPlaying = hasTrack && media.IsPlaying;

        if (_isMediaActive && _isMediaPlaying && SettingsService.Current.Avatar.IdleAnimation && SettingsService.Current.Behavior.MusicReactions)
        {
            if (!_danceTimer.IsEnabled) _danceTimer.Start();
        }
        else
        {
            if (_danceTimer.IsEnabled)
            {
                DanceTimer_Tick();
            }
        }

        // DJ Headphone Accessory: automatically wears headphones when playing!
        SetHeadphonesVisible(_isMediaPlaying);

        // Speech Bubble: pop up new song title in Mini Mode & sweep Liquid Color Wave across notch
        string trackSig = $"{media.Title}|{media.Artist}";
        if (hasTrack && trackSig != _lastTrackSignature)
        {
            _lastTrackSignature = trackSig;
            TriggerLiquidColorWave(_currentAccentColor);
            if (_isAvatarMiniMode && media.IsPlaying)
            {
                ShowMiniSpeechBubble("🎵", $"{media.Title} — {media.Artist}");
            }
        }

        if (_isAvatarMiniMode)
        {
            if (hasTrack && media.IsPlaying)
            {
                if (media.Thumbnail != null)
                {
                    if (!ReferenceEquals(_lastAnalyzedCover, media.Thumbnail))
                    {
                        _lastAnalyzedCover = media.Thumbnail;
                        Color dominant = AdaptiveColorService.ExtractDominantColor(media.Thumbnail);
                        ApplyAdaptiveAccentColor(dominant);
                    }
                }
            }
            else
            {
                // Music paused or stopped in Mini Mode -> revert to wallpaper ambient color
                if (_lastAnalyzedCover != null)
                {
                    _lastAnalyzedCover = null;
                    _lastAppliedAmbientColor = Colors.Transparent;
                    CheckAndUpdateAmbientColor();
                }
            }
            return;
        }

        if (hasTrack)
        {
            if (media.Thumbnail != null)
            {
                MiniThumbBrush.ImageSource = media.Thumbnail;
                MiniThumbBorder.Visibility = Visibility.Visible;
                MiniStatusDot.Visibility = Visibility.Collapsed;

                if (!ReferenceEquals(_lastAnalyzedCover, media.Thumbnail))
                {
                    _lastAnalyzedCover = media.Thumbnail;
                    Color dominant = AdaptiveColorService.ExtractDominantColor(media.Thumbnail);
                    ApplyAdaptiveAccentColor(dominant);
                }
            }
            else
            {
                MiniThumbBrush.ImageSource = null;
                MiniThumbBorder.Visibility = Visibility.Collapsed;
                MiniStatusDot.Visibility = Visibility.Visible;

                if (_lastAnalyzedCover != null)
                {
                    _lastAnalyzedCover = null;
                    var ambient = SampleCurrentAmbientColorAroundIsland();
                    _lastAppliedAmbientColor = ambient;
                    ApplyAdaptiveAccentColor(ambient);
                }
            }

            EqualizerPanel.Visibility = media.IsPlaying ? Visibility.Visible : Visibility.Collapsed;

            // Hide secondary right-side capsules when song is playing so Song Title & Artist have ~235px of clean space
            MiniVolBorder.Visibility = Visibility.Collapsed;
            MiniWeatherBorder.Visibility = Visibility.Collapsed;
            MiniNetBorder.Visibility = Visibility.Collapsed;

            if (_isTimerRunning)
            {
                int m = _timerRemainingSeconds / 60;
                int s = _timerRemainingSeconds % 60;
                MiniMediaInfoPanel.Visibility = Visibility.Collapsed;
                CollapsedCenterGrid.Visibility = Visibility.Visible;
                MiniCenterText.Visibility = Visibility.Visible;
                MiniCenterText.Text = $"⏱️ {m:00}:{s:00}  •  {media.Title}";
            }
            else
            {
                CollapsedCenterGrid.Visibility = Visibility.Collapsed;
                MiniCenterText.Visibility = Visibility.Collapsed;
                MiniMediaInfoPanel.Visibility = Visibility.Visible;
                MiniTrackTitleText.Text = media.Title;
                MiniTrackTitleText.ToolTip = $"{media.Title} — {media.Artist}";
                if (!string.IsNullOrWhiteSpace(media.Artist))
                {
                    MiniTrackArtistText.Text = media.Artist;
                    MiniTrackArtistText.ToolTip = $"{media.Title} — {media.Artist}";
                    MiniTrackArtistText.Visibility = Visibility.Visible;
                }
                else
                {
                    MiniTrackArtistText.Visibility = Visibility.Collapsed;
                }
            }

            ExpandedTrackTitle.Text = media.Title;
            ExpandedArtist.Text = media.Artist;
            MediaAppText.Text = media.SourceApp;
            UpdatePlayPauseVectorIcon(media.IsPlaying);

            if (media.Thumbnail != null)
            {
                LargeThumbBrush.ImageSource = media.Thumbnail;
                LargeThumbVisualBorder.Visibility = Visibility.Visible;
                NoCoverFallback.Visibility = Visibility.Collapsed;
            }
            else
            {
                LargeThumbBrush.ImageSource = null;
                LargeThumbVisualBorder.Visibility = Visibility.Collapsed;
                NoCoverFallback.Visibility = Visibility.Visible;
            }

            if (media.Duration > TimeSpan.Zero)
            {
                TrackSeekSlider.IsEnabled = true;
                TrackSeekSlider.Maximum = Math.Max(1, media.Duration.TotalSeconds);
                TotalTimeText.Text = FormatMediaTime(media.Duration);

                if (!_isDraggingSeekSlider)
                {
                    TrackSeekSlider.Value = Math.Clamp(media.Position.TotalSeconds, 0, TrackSeekSlider.Maximum);
                    CurrentTimeText.Text = FormatMediaTime(media.Position);
                }
            }
            else
            {
                TrackSeekSlider.IsEnabled = false;
                TrackSeekSlider.Maximum = 100;
                TrackSeekSlider.Value = 0;
                CurrentTimeText.Text = "00:00";
                TotalTimeText.Text = "--:--";
            }

            if (trackSig != _lastLyricsTrackSignature)
            {
                _lastLyricsTrackSignature = trackSig;
                _currentLyrics = null;
                _lastActiveLyricIndex = -2;
                LyricsOverlayTitle.Text = media.Title;
                LyricsOverlayArtist.Text = string.IsNullOrWhiteSpace(media.Artist) ? "" : $" • {media.Artist}";
                LiveCurrentLyricText.Text = "Şarkı sözleri aranıyor...";
                _ = FetchLyricsAsync(media.Title, media.Artist, media.Duration);
            }

            UpdateLiveLyricsDisplay(media.Position);
        }
        else
        {
            LiveCurrentLyricText.Text = "Şarkı bekleniyor...";
            LyricsOverlayTitle.Text = "Şarkı Sözleri";
            LyricsOverlayArtist.Text = "";

            MiniThumbBrush.ImageSource = null;
            MiniThumbBorder.Visibility = Visibility.Collapsed;
            MiniStatusDot.Visibility = Visibility.Visible;
            EqualizerPanel.Visibility = Visibility.Collapsed;
            MiniMediaInfoPanel.Visibility = Visibility.Collapsed;
            CollapsedCenterGrid.Visibility = Visibility.Visible;
            MiniCenterText.Visibility = Visibility.Visible;
            MiniVolBorder.Visibility = Visibility.Visible;

            if (_lastAnalyzedCover != null)
            {
                _lastAnalyzedCover = null;
                var ambient = SampleCurrentAmbientColorAroundIsland();
                _lastAppliedAmbientColor = ambient;
                ApplyAdaptiveAccentColor(ambient);
            }

            if (_isTimerRunning)
            {
                int m = _timerRemainingSeconds / 60;
                int s = _timerRemainingSeconds % 60;
                MiniCenterText.Text = $"⏱️ {m:00}:{s:00} Pomodoro";
            }
            else
            {
                MiniCenterText.Text = $"{timeStr}  •  {dayStr}";
            }

            ExpandedTrackTitle.Text = "Çalan medya bulunamadı";
            ExpandedArtist.Text = "Spotify, Tarayıcı veya Media Player'dan bir şey açın";
            MediaAppText.Text = "Bekleniyor";
            UpdatePlayPauseVectorIcon(false);
            LargeThumbBrush.ImageSource = null;
            LargeThumbVisualBorder.Visibility = Visibility.Collapsed;
            NoCoverFallback.Visibility = Visibility.Visible;
            TrackSeekSlider.IsEnabled = false;
            TrackSeekSlider.Value = 0;
            CurrentTimeText.Text = "00:00";
            TotalTimeText.Text = "00:00";
        }
    }

    private static readonly Geometry PauseVectorGeometry = Geometry.Parse("M 7,5 L 10.2,5 L 10.2,19 L 7,19 Z M 13.8,5 L 17,5 L 17,19 L 13.8,19 Z");
    private static readonly Geometry PlayVectorGeometry = Geometry.Parse("M 7.5,4.5 L 18.5,12 L 7.5,19.5 Z");
    private static readonly Geometry MiniPauseVectorGeometry = Geometry.Parse("M 7,5 L 9.5,5 L 9.5,15 L 7,15 Z M 12.5,5 L 15,5 L 15,15 L 12.5,15 Z");
    private static readonly Geometry MiniPlayVectorGeometry = Geometry.Parse("M 8,5 L 15.5,10 L 8,15 Z");
    private Color _currentAccentColor = AdaptiveColorService.DefaultAccent;

    private void UpdatePlayPauseVectorIcon(bool isPlaying)
    {
        if (PlayPauseIconPath != null)
            PlayPauseIconPath.Data = isPlaying ? PauseVectorGeometry : PlayVectorGeometry;
        if (MiniPlayPausePath != null)
            MiniPlayPausePath.Data = isPlaying ? MiniPauseVectorGeometry : MiniPlayVectorGeometry;
    }

    private static string FormatMediaTime(TimeSpan ts)
    {
        if (ts < TimeSpan.Zero) ts = TimeSpan.Zero;
        return ts.TotalHours >= 1
            ? ts.ToString(@"hh\:mm\:ss")
            : ts.ToString(@"mm\:ss");
    }

    private static Color EnsureAvatarVibrancy(Color c)
    {
        // If color is uninitialized, default black or very dark, fallback to Onee's iconic golden yellow
        if (c.A == 0 || (c.R < 35 && c.G < 35 && c.B < 35))
        {
            return Color.FromRgb(255, 228, 103); // #FFE4D167
        }

        double lum = (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0;
        if (lum < 0.45)
        {
            double factor = 0.65 / Math.Max(0.08, lum);
            byte r = (byte)Math.Clamp((int)(c.R * factor + 45), 140, 255);
            byte g = (byte)Math.Clamp((int)(c.G * factor + 35), 110, 245);
            byte b = (byte)Math.Clamp((int)(c.B * factor + 45), 120, 255);
            return Color.FromRgb(r, g, b);
        }
        return c;
    }

    private void UpdateAvatarBrushes(Color accent)
    {
        var porcelainBrush = new LinearGradientBrush(
            Color.FromRgb(255, 255, 255),
            Color.FromRgb(228, 235, 245),
            new Point(0.2, 0.0),
            new Point(0.8, 1.0));
        porcelainBrush.GradientStops.Add(new GradientStop(Color.FromRgb(255, 255, 255), 0.0));
        porcelainBrush.GradientStops.Add(new GradientStop(Color.FromRgb(248, 250, 252), 0.45));
        porcelainBrush.GradientStops.Add(new GradientStop(Color.FromRgb(226, 232, 242), 1.0));
        porcelainBrush.Freeze();

        if (MiniBubbleHeadPath != null) MiniBubbleHeadPath.Fill = porcelainBrush;
        if (MiniAvatarHeadPath != null) MiniAvatarHeadPath.Fill = porcelainBrush;
        if (LargeAvatarHeadPath != null) LargeAvatarHeadPath.Fill = porcelainBrush;
        if (ExpandedStageHeadPath != null) ExpandedStageHeadPath.Fill = porcelainBrush;
        if (OneeLeftHand != null) OneeLeftHand.Fill = porcelainBrush;
        if (OneeRightHand != null) OneeRightHand.Fill = porcelainBrush;
        if (MiniBubbleLeftHand != null) MiniBubbleLeftHand.Fill = porcelainBrush;
        if (MiniBubbleRightHand != null) MiniBubbleRightHand.Fill = porcelainBrush;
        if (ExpandedStageLeftHand != null) ExpandedStageLeftHand.Fill = porcelainBrush;
        if (ExpandedStageRightHand != null) ExpandedStageRightHand.Fill = porcelainBrush;

        if (MochiSpotlightColorStop != null)
        {
            MochiSpotlightColorStop.Color = Color.FromArgb(90, accent.R, accent.G, accent.B);
        }

        var res = System.Windows.Application.Current?.Resources;
        if (res != null)
        {
            res["DynamicAvatarBodyBrush"] = porcelainBrush;
        }
    }

    private void ApplyAdaptiveAccentColor(Color accent)
    {
        // If the color passed in is essentially black/dark gray (e.g. headless or pure black desktop), fallback to DefaultAccent
        double brightness = (0.299 * accent.R + 0.587 * accent.G + 0.114 * accent.B) / 255.0;
        if (accent.A == 0 || brightness < 0.22 || (accent.R < 45 && accent.G < 45 && accent.B < 45))
        {
            accent = AdaptiveColorService.DefaultAccent;
        }

        if (_themeOverrideColor != null)
        {
            accent = _themeOverrideColor.Value;
        }
        else if (!SettingsService.Current.Appearance.AdaptiveGlow && _currentAccentColor != Colors.Transparent)
        {
            accent = _currentAccentColor;
        }

        _currentAccentColor = accent;

        var lighter = Color.FromRgb(
            (byte)Math.Min(255, accent.R + 42),
            (byte)Math.Min(255, accent.G + 42),
            (byte)Math.Min(255, accent.B + 42));

        var darker = Color.FromRgb(
            (byte)(accent.R * 0.62),
            (byte)(accent.G * 0.62),
            (byte)(accent.B * 0.62));

        // Complementary secondary adaptive tone (shifted slightly toward warm/magenta-violet for RAM card harmony)
        var secondaryAccent = Color.FromRgb(
            (byte)Math.Clamp((int)(accent.R * 0.78 + 55), 60, 255),
            (byte)Math.Clamp((int)(accent.G * 0.68 + 20), 40, 240),
            (byte)Math.Clamp((int)(accent.B * 0.82 + 45), 80, 255));
        var secondaryLighter = Color.FromRgb(
            (byte)Math.Min(255, secondaryAccent.R + 38),
            (byte)Math.Min(255, secondaryAccent.G + 38),
            (byte)Math.Min(255, secondaryAccent.B + 38));

        // 1. Update Sliders (Seek Bar, Volume Bar, Brightness Slider) and Play Circle Border via DynamicResources
        var res = System.Windows.Application.Current.Resources;
        res["DynamicAccentBrush"] = new SolidColorBrush(accent);
        res["DynamicAccentSoftBrush"] = new SolidColorBrush(Color.FromArgb(65, accent.R, accent.G, accent.B));
        res["DynamicAccentBorderBrush"] = new SolidColorBrush(Color.FromArgb(205, lighter.R, lighter.G, lighter.B));
        res["DynamicAvatarBodyBrush"] = new SolidColorBrush(EnsureAvatarVibrancy(accent));

        UpdateAvatarBrushes(accent);

        var playRadial = new RadialGradientBrush
        {
            Center = new System.Windows.Point(0.5, 0.5),
            GradientOrigin = new System.Windows.Point(0.5, 0.35),
            RadiusX = 0.65,
            RadiusY = 0.65
        };
        playRadial.GradientStops.Add(new GradientStop(Color.FromArgb(215, accent.R, accent.G, accent.B), 0.0));
        playRadial.GradientStops.Add(new GradientStop(Color.FromArgb(130, darker.R, darker.G, darker.B), 1.0));
        res["DynamicPlayFillBrush"] = playRadial;

        // 2. Collapsed Main Island & Equalizer bars & Album border
        MiniStatusHalo.Fill = new SolidColorBrush(Color.FromArgb(58, accent.R, accent.G, accent.B));
        MiniStatusCore.Fill = new SolidColorBrush(lighter);
        MiniTrackArtistText.Foreground = new SolidColorBrush(Color.FromRgb(
            (byte)Math.Min(255, 148 + accent.R / 5),
            (byte)Math.Min(255, 158 + accent.G / 5),
            (byte)Math.Min(255, 174 + accent.B / 5)));
        EqBar1.Fill = new SolidColorBrush(accent);
        EqBar2.Fill = new SolidColorBrush(lighter);
        EqBar3.Fill = new SolidColorBrush(secondaryLighter);
        LargeAlbumBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(145, lighter.R, lighter.G, lighter.B));
        MediaControlsCardBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(62, accent.R, accent.G, accent.B));
        TimerStartPauseBtn.Background = new SolidColorBrush(Color.FromArgb(180, accent.R, accent.G, accent.B));

        if (_isCurrentTrackFavorited)
        {
            var favBrush = new SolidColorBrush(accent);
            HeartIconPath.Fill = favBrush;
            HeartIconPath.Stroke = favBrush;
        }

        // 3. Smoothly animate the 360-Degree Island Rim Border and Dual Ambient Radial Glows
        double glowFactor = Math.Clamp(SettingsService.Current.Appearance.IslandGlow / 100.0, 0.0, 1.0);
        var animDur = TimeSpan.FromMilliseconds(SettingsService.Current.Performance.ReduceMotion ? 100 : 650);

        IslandBorderAccentStop.BeginAnimation(GradientStop.ColorProperty,
            new ColorAnimation(Color.FromArgb((byte)(185 * glowFactor), accent.R, accent.G, accent.B), animDur));
        IslandBorderMidStop.BeginAnimation(GradientStop.ColorProperty,
            new ColorAnimation(Color.FromArgb((byte)(88 * glowFactor), accent.R, accent.G, accent.B), animDur));
        IslandBorderTopStop.BeginAnimation(GradientStop.ColorProperty,
            new ColorAnimation(Color.FromArgb((byte)(72 * glowFactor), secondaryLighter.R, secondaryLighter.G, secondaryLighter.B), animDur));

        AmbientGlowStop.BeginAnimation(GradientStop.ColorProperty,
            new ColorAnimation(Color.FromArgb((byte)(64 * glowFactor), accent.R, accent.G, accent.B), animDur));
        AmbientGlowMidStop.BeginAnimation(GradientStop.ColorProperty,
            new ColorAnimation(Color.FromArgb((byte)(26 * glowFactor), accent.R, accent.G, accent.B), animDur));
        AmbientGlowTopRightStop.BeginAnimation(GradientStop.ColorProperty,
            new ColorAnimation(Color.FromArgb((byte)(44 * glowFactor), secondaryAccent.R, secondaryAccent.G, secondaryAccent.B), animDur));

        // Adaptive Obsidian-Glass Tint
        if (IslandBgAccentStop != null)
        {
            IslandBgAccentStop.BeginAnimation(GradientStop.ColorProperty,
                new ColorAnimation(Color.FromArgb(240, (byte)Math.Clamp(6 + accent.R * 0.08, 0, 42), (byte)Math.Clamp(8 + accent.G * 0.08, 0, 42), (byte)Math.Clamp(14 + accent.B * 0.08, 0, 48)), animDur));
        }
        if (IslandBgMidStop != null)
        {
            IslandBgMidStop.BeginAnimation(GradientStop.ColorProperty,
                new ColorAnimation(Color.FromArgb(248, (byte)Math.Clamp(10 + accent.R * 0.04, 0, 32), (byte)Math.Clamp(14 + accent.G * 0.04, 0, 32), (byte)Math.Clamp(22 + accent.B * 0.04, 0, 38)), animDur));
        }

        // 4. Adapt Avatar Stage Spotlight to island theme while keeping Mochi soft porcelain 3D
        UpdateAvatarBrushes(accent);

        if (StageGlowCoreStop != null)
        {
            StageGlowCoreStop.BeginAnimation(GradientStop.ColorProperty,
                new ColorAnimation(Color.FromArgb((byte)(110 * glowFactor), accent.R, accent.G, accent.B), animDur));
        }
        if (StageGlowMidStop != null)
        {
            StageGlowMidStop.BeginAnimation(GradientStop.ColorProperty,
                new ColorAnimation(Color.FromArgb((byte)(35 * glowFactor), accent.R, accent.G, accent.B), animDur));
        }
        if (LeftEarGlowEffect != null)
        {
            LeftEarGlowEffect.BeginAnimation(DropShadowEffect.ColorProperty,
                new ColorAnimation(accent, animDur));
        }
        if (RightEarGlowEffect != null)
        {
            RightEarGlowEffect.BeginAnimation(DropShadowEffect.ColorProperty,
                new ColorAnimation(accent, animDur));
        }
        if (NotchBottomVolumeFill != null)
        {
            NotchBottomVolumeFill.Background = new SolidColorBrush(accent);
        }

        // Liquid Color Wave shimmer flowing across the notch from left ear to right ear!
        TriggerLiquidColorWave(accent);

        MediaCardGlowStop.BeginAnimation(GradientStop.ColorProperty,
            new ColorAnimation(Color.FromArgb(56, accent.R, accent.G, accent.B), animDur));

        // 4. Update TAB 2 (⚡ Sistem) 5 Cards with Adaptive Colors!
        CpuCardGlowStop.BeginAnimation(GradientStop.ColorProperty,
            new ColorAnimation(Color.FromArgb(58, accent.R, accent.G, accent.B), animDur));
        GpuCardGlowStop.BeginAnimation(GradientStop.ColorProperty,
            new ColorAnimation(Color.FromArgb(58, accent.R, accent.G, accent.B), animDur));
        RamCardGlowStop.BeginAnimation(GradientStop.ColorProperty,
            new ColorAnimation(Color.FromArgb(58, secondaryAccent.R, secondaryAccent.G, secondaryAccent.B), animDur));
        PowerCardGlowStop.BeginAnimation(GradientStop.ColorProperty,
            new ColorAnimation(Color.FromArgb(54, accent.R, accent.G, accent.B), animDur));
        EnvCardGlowStop.BeginAnimation(GradientStop.ColorProperty,
            new ColorAnimation(Color.FromArgb(60, lighter.R, lighter.G, lighter.B), animDur));

        StatCpuCardBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(62, accent.R, accent.G, accent.B));
        StatGpuCardBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(62, accent.R, accent.G, accent.B));
        StatRamCardBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(62, secondaryAccent.R, secondaryAccent.G, secondaryAccent.B));
        StatPowerCardBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(62, accent.R, accent.G, accent.B));
        StatEnvCardBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(62, lighter.R, lighter.G, lighter.B));

        CpuIconBadge.Background = new SolidColorBrush(Color.FromArgb(44, accent.R, accent.G, accent.B));
        CpuIconBadge.BorderBrush = new SolidColorBrush(Color.FromArgb(85, accent.R, accent.G, accent.B));
        CpuIconPath.Stroke = new SolidColorBrush(lighter);
        CpuSparkText.Foreground = new SolidColorBrush(lighter);

        GpuIconBadge.Background = new SolidColorBrush(Color.FromArgb(44, accent.R, accent.G, accent.B));
        GpuIconBadge.BorderBrush = new SolidColorBrush(Color.FromArgb(85, accent.R, accent.G, accent.B));
        GpuIconPath.Stroke = new SolidColorBrush(lighter);
        GpuSparkText.Foreground = new SolidColorBrush(lighter);
        StatGpuBar.Foreground = new SolidColorBrush(accent);

        RamIconBadge.Background = new SolidColorBrush(Color.FromArgb(44, secondaryAccent.R, secondaryAccent.G, secondaryAccent.B));
        RamIconBadge.BorderBrush = new SolidColorBrush(Color.FromArgb(85, secondaryAccent.R, secondaryAccent.G, secondaryAccent.B));
        RamIconPath.Stroke = new SolidColorBrush(secondaryLighter);
        RamSparkText.Foreground = new SolidColorBrush(secondaryLighter);
        StatRamBar.Foreground = new SolidColorBrush(secondaryAccent);

        PowerIconBadge.Background = new SolidColorBrush(Color.FromArgb(44, accent.R, accent.G, accent.B));
        PowerIconBadge.BorderBrush = new SolidColorBrush(Color.FromArgb(85, accent.R, accent.G, accent.B));
        PowerIconPath.Stroke = new SolidColorBrush(lighter);

        // 5-Bar CPU History Bars
        CpuHistBar1.Fill = new SolidColorBrush(Color.FromArgb(105, accent.R, accent.G, accent.B));
        CpuHistBar2.Fill = new SolidColorBrush(Color.FromArgb(140, accent.R, accent.G, accent.B));
        CpuHistBar3.Fill = new SolidColorBrush(Color.FromArgb(175, accent.R, accent.G, accent.B));
        CpuHistBar4.Fill = new SolidColorBrush(Color.FromArgb(215, accent.R, accent.G, accent.B));
        CpuHistBar5.Fill = new SolidColorBrush(lighter);

        // 5-Bar GPU History Bars
        GpuHistBar1.Fill = new SolidColorBrush(Color.FromArgb(105, accent.R, accent.G, accent.B));
        GpuHistBar2.Fill = new SolidColorBrush(Color.FromArgb(140, accent.R, accent.G, accent.B));
        GpuHistBar3.Fill = new SolidColorBrush(Color.FromArgb(175, accent.R, accent.G, accent.B));
        GpuHistBar4.Fill = new SolidColorBrush(Color.FromArgb(215, accent.R, accent.G, accent.B));
        GpuHistBar5.Fill = new SolidColorBrush(lighter);

        // 5-Bar RAM History Bars
        RamHistBar1.Fill = new SolidColorBrush(Color.FromArgb(105, secondaryAccent.R, secondaryAccent.G, secondaryAccent.B));
        RamHistBar2.Fill = new SolidColorBrush(Color.FromArgb(140, secondaryAccent.R, secondaryAccent.G, secondaryAccent.B));
        RamHistBar3.Fill = new SolidColorBrush(Color.FromArgb(175, secondaryAccent.R, secondaryAccent.G, secondaryAccent.B));
        RamHistBar4.Fill = new SolidColorBrush(Color.FromArgb(215, secondaryAccent.R, secondaryAccent.G, secondaryAccent.B));
        RamHistBar5.Fill = new SolidColorBrush(secondaryLighter);

        // Weather Cloud & Wi-Fi Signal Bars
        WeatherDecorCloudPath.Fill = new SolidColorBrush(Color.FromArgb(64, lighter.R, lighter.G, lighter.B));
        var wifiBrush = new SolidColorBrush(lighter);
        WifiBar1.Fill = wifiBrush;
        WifiBar2.Fill = wifiBrush;
        WifiBar3.Fill = wifiBrush;
        WifiBar4.Fill = wifiBrush;

        // 5. Update TAB 1 (Tepsi), TAB 3 (Sayaç), TAB 4 (Dock) Cards with Adaptive Colors!
        TrayCardGlowStop.BeginAnimation(GradientStop.ColorProperty,
            new ColorAnimation(Color.FromArgb(52, accent.R, accent.G, accent.B), animDur));
        ClipboardCardGlowStop.BeginAnimation(GradientStop.ColorProperty,
            new ColorAnimation(Color.FromArgb(52, secondaryAccent.R, secondaryAccent.G, secondaryAccent.B), animDur));
        TimerCardGlowStop.BeginAnimation(GradientStop.ColorProperty,
            new ColorAnimation(Color.FromArgb(54, accent.R, accent.G, accent.B), animDur));
        NotesCardGlowStop.BeginAnimation(GradientStop.ColorProperty,
            new ColorAnimation(Color.FromArgb(52, secondaryAccent.R, secondaryAccent.G, secondaryAccent.B), animDur));
        DockCardGlowStop.BeginAnimation(GradientStop.ColorProperty,
            new ColorAnimation(Color.FromArgb(52, accent.R, accent.G, accent.B), animDur));
        SettingsCardGlowStop.BeginAnimation(GradientStop.ColorProperty,
            new ColorAnimation(Color.FromArgb(52, secondaryAccent.R, secondaryAccent.G, secondaryAccent.B), animDur));

        TrayCardBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(62, accent.R, accent.G, accent.B));
        ClipboardCardBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(62, secondaryAccent.R, secondaryAccent.G, secondaryAccent.B));
        TimerCardBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(62, accent.R, accent.G, accent.B));
        NotesCardBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(62, secondaryAccent.R, secondaryAccent.G, secondaryAccent.B));
        DockCardBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(62, accent.R, accent.G, accent.B));
        SettingsCardBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(62, secondaryAccent.R, secondaryAccent.G, secondaryAccent.B));

        TimerRingArcPath.Stroke = new SolidColorBrush(lighter);
        TimerTagText.Foreground = new SolidColorBrush(lighter);

        // 6. Refresh active tab highlight & pin/stealth buttons with new accent color
        UpdatePinButtonVisual();
        UpdateStealthModeVisual();
        UpdateTabButtonsStyle(_activeTabIndex);
    }

    private void UpdateStatsUi(SystemStatsSnapshot stats)
    {
        var lighterAccent = Color.FromRgb(
            (byte)Math.Min(255, _currentAccentColor.R + 45),
            (byte)Math.Min(255, _currentAccentColor.G + 45),
            (byte)Math.Min(255, _currentAccentColor.B + 45));

        MiniBatteryText.Text = stats.HasBattery ? $"⚡ {stats.BatteryPercent}%" : "⚡ AC";
        MiniBatteryText.Foreground = stats.BatteryPercent < 20 && !stats.IsCharging
            ? new SolidColorBrush(Color.FromRgb(255, 100, 100))
            : new SolidColorBrush(lighterAccent);

        // Detect charging plugged in transition
        if (_lastChargingState.HasValue && !_lastChargingState.Value && stats.IsCharging && stats.HasBattery)
        {
            ShowToastBanner("⚡", "Şarj Ediliyor", $"%{stats.BatteryPercent} Güç Bağlandı");
        }
        _lastChargingState = stats.IsCharging;
        _lastSystemStats = stats;

        // Living Desktop Pet reaction: Critical Low Battery (< 15%)
        if (stats.HasBattery && stats.BatteryPercent <= 15 && !stats.IsCharging)
        {
            if (!_hasAlertedLowBattery)
            {
                _hasAlertedLowBattery = true;
                TriggerAvatarReaction(AvatarState.Sad, 6);
                if (_isAvatarMiniMode)
                {
                    ShowMiniSpeechBubble("⚡", $"Karnım acıktı! Beni şarja tak (%{stats.BatteryPercent})");
                }
            }
        }
        else if (stats.IsCharging || stats.BatteryPercent > 20)
        {
            _hasAlertedLowBattery = false;
        }

        // Living Desktop Pet reaction: High CPU or GPU Load (>= 85%)
        if (stats.CpuPercent >= 85 || stats.GpuPercent >= 85)
        {
            if (!_hasAlertedHighLoad)
            {
                _hasAlertedHighLoad = true;
                TriggerAvatarReaction(AvatarState.Drowsy, 6);
                if (_isAvatarMiniMode)
                {
                    string highLoadText = stats.CpuPercent >= 85
                        ? $"Çok yoruldum! CPU %{stats.CpuPercent}"
                        : $"Çok yoruldum! GPU %{stats.GpuPercent}";
                    ShowMiniSpeechBubble("🥵", highLoadText);
                }
            }
        }
        else if (stats.CpuPercent < 75 && stats.GpuPercent < 75)
        {
            _hasAlertedHighLoad = false;
        }

        // Card 1: CPU
        StatCpuModelText.Text = stats.CpuModelName;
        StatCpuPercentText.Text = $"{stats.CpuPercent}%";
        StatCpuBar.Value = stats.CpuPercent;
        StatCpuFreqText.Text = stats.CpuFrequencyText;
        StatCpuTempText.Text = stats.CpuTempText;

        if (stats.CpuHistory.Length >= 5)
        {
            CpuHistScale1.ScaleY = Math.Clamp(stats.CpuHistory[0] / 100.0, 0.18, 1.0);
            CpuHistScale2.ScaleY = Math.Clamp(stats.CpuHistory[1] / 100.0, 0.18, 1.0);
            CpuHistScale3.ScaleY = Math.Clamp(stats.CpuHistory[2] / 100.0, 0.18, 1.0);
            CpuHistScale4.ScaleY = Math.Clamp(stats.CpuHistory[3] / 100.0, 0.18, 1.0);
            CpuHistScale5.ScaleY = Math.Clamp(stats.CpuHistory[4] / 100.0, 0.18, 1.0);
        }

        // Card 2: GPU
        StatGpuModelText.Text = stats.GpuModelName;
        StatGpuPercentText.Text = $"{stats.GpuPercent}%";
        StatGpuBar.Value = stats.GpuPercent;
        StatGpuVramFooterText.Text = stats.GpuMemoryText;
        StatGpuTempText.Text = stats.GpuTempText;

        if (stats.GpuHistory.Length >= 5)
        {
            GpuHistScale1.ScaleY = Math.Clamp(stats.GpuHistory[0] / 100.0, 0.16, 1.0);
            GpuHistScale2.ScaleY = Math.Clamp(stats.GpuHistory[1] / 100.0, 0.16, 1.0);
            GpuHistScale3.ScaleY = Math.Clamp(stats.GpuHistory[2] / 100.0, 0.16, 1.0);
            GpuHistScale4.ScaleY = Math.Clamp(stats.GpuHistory[3] / 100.0, 0.16, 1.0);
            GpuHistScale5.ScaleY = Math.Clamp(stats.GpuHistory[4] / 100.0, 0.16, 1.0);
        }

        // Card 2: RAM
        StatRamPercentText.Text = $"{stats.RamPercent}%";
        StatRamDetailText.Text = $"{stats.RamUsedGb:0.0} / {stats.RamTotalGb:0.0} GB";
        StatRamUsedFooterText.Text = $"{stats.RamUsedGb:0.0} GB Kullanımda";
        StatRamBar.Value = stats.RamPercent;

        if (stats.RamHistory.Length >= 5)
        {
            RamHistScale1.ScaleY = Math.Clamp(stats.RamHistory[0] / 100.0, 0.18, 1.0);
            RamHistScale2.ScaleY = Math.Clamp(stats.RamHistory[1] / 100.0, 0.18, 1.0);
            RamHistScale3.ScaleY = Math.Clamp(stats.RamHistory[2] / 100.0, 0.18, 1.0);
            RamHistScale4.ScaleY = Math.Clamp(stats.RamHistory[3] / 100.0, 0.18, 1.0);
            RamHistScale5.ScaleY = Math.Clamp(stats.RamHistory[4] / 100.0, 0.18, 1.0);
        }

        // Card 3: Pil & Güç + Ekran Parlaklığı
        StatBatteryPercentText.Text = $"{stats.BatteryPercent}%";
        StatBatteryStatusText.Text = stats.HasBattery
            ? (stats.IsCharging ? "Takılı (Şarj Ediliyor) ⚡" : "Pilde Çalışıyor")
            : "Takılı (AC Güç) ⚡";
        StatBatteryBar.Value = stats.BatteryPercent;

        // Card 4: Live Internet Download / Upload Speed & Bluetooth
        StatNetSpeedText.Text = !string.IsNullOrEmpty(stats.CompactDownloadText)
            ? stats.CompactDownloadText
            : stats.NetworkSpeedSummary.Split('•')[0].Trim();

        if (!string.IsNullOrEmpty(stats.CompactDownloadText) && !_isMediaActive)
        {
            MiniNetText.Text = stats.CompactDownloadText;
            MiniNetBorder.Visibility = Visibility.Visible;
        }
        else
        {
            MiniNetBorder.Visibility = Visibility.Collapsed;
        }

        StatBluetoothText.Text = stats.BluetoothSummary;
        StatNetworkText.Text = stats.NetworkSummary;
    }

    private void UpdateWeatherUi(WeatherSnapshot weather)
    {
        if (weather.IsLoaded)
        {
            MiniWeatherText.Text = weather.CompactSummary;
            MiniWeatherBorder.Visibility = _isMediaActive ? Visibility.Collapsed : Visibility.Visible;
            StatWeatherIconText.Text = weather.Emoji;
            StatWeatherCityText.Text = string.IsNullOrWhiteSpace(weather.City) ? "Konum" : weather.City;
            StatWeatherTitle.Text = $"{weather.TemperatureText} • {weather.ConditionText}";
            UpdateAtmosphericWeather(weather);
        }
    }

    private void WeatherCityHeader_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        WeatherCityInputBox.Text = StatWeatherCityText.Text == "Konum..." || StatWeatherCityText.Text == "Konum"
            ? ""
            : StatWeatherCityText.Text;
        WeatherCityDisplayPanel.Visibility = Visibility.Collapsed;
        WeatherCityEditPanel.Visibility = Visibility.Visible;
        WeatherCityInputBox.Focus();
        WeatherCityInputBox.SelectAll();
        e.Handled = true;
    }

    private async void WeatherCityInputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            await ApplyCustomWeatherCityAsync(WeatherCityInputBox.Text);
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            WeatherCityEditPanel.Visibility = Visibility.Collapsed;
            WeatherCityDisplayPanel.Visibility = Visibility.Visible;
        }
    }

    private async void WeatherCitySave_Click(object sender, RoutedEventArgs e)
    {
        await ApplyCustomWeatherCityAsync(WeatherCityInputBox.Text);
    }

    private async void WeatherCityAutoGps_Click(object sender, RoutedEventArgs e)
    {
        WeatherCityEditPanel.Visibility = Visibility.Collapsed;
        WeatherCityDisplayPanel.Visibility = Visibility.Visible;
        StatWeatherTitle.Text = "GPS konumu alınıyor...";

        _savedState.CustomWeatherCity = null;
        _savedState.CustomWeatherLat = null;
        _savedState.CustomWeatherLon = null;
        StorageService.Save(_savedState);

        _weatherService.ConfigureCustomLocation(null, null, null);
        var weather = await _weatherService.GetWeatherAsync();
        UpdateWeatherUi(weather);
        ShowToastBanner("📍", "GPS Konumu Güncellendi", $"{weather.City} • {weather.TemperatureText}");
    }

    private async Task ApplyCustomWeatherCityAsync(string query)
    {
        WeatherCityEditPanel.Visibility = Visibility.Collapsed;
        WeatherCityDisplayPanel.Visibility = Visibility.Visible;

        if (string.IsNullOrWhiteSpace(query))
        {
            return;
        }

        StatWeatherTitle.Text = "Şehir aranıyor...";
        var (success, cityName, lat, lon) = await _weatherService.SearchCityCoordinatesAsync(query);
        if (success)
        {
            _savedState.CustomWeatherCity = cityName;
            _savedState.CustomWeatherLat = lat;
            _savedState.CustomWeatherLon = lon;
            StorageService.Save(_savedState);

            _weatherService.ConfigureCustomLocation(cityName, lat, lon);
            var weather = await _weatherService.GetWeatherAsync();
            UpdateWeatherUi(weather);
            ShowToastBanner("🌤️", "Konum Değiştirildi", $"{weather.City} • {weather.TemperatureText}");
        }
        else
        {
            ShowToastBanner("⚠️", "Şehir Bulunamadı", $"'{query}' bulunamadı");
            var weather = await _weatherService.GetWeatherAsync();
            UpdateWeatherUi(weather);
        }
    }

    public void ShowToastBanner(string icon, string title, string message)
    {
        if (!SettingsService.Current.Behavior.ShowNotifications) return;

        if (_isAvatarMiniMode)
        {
            TriggerAvatarReaction(AvatarState.Surprised, 3);
            return;
        }

        ToastIconText.Text = icon;
        ToastTitleText.Text = title;
        ToastMessageText.Text = message;
        ToastBannerOverlay.Visibility = Visibility.Visible;

        SoundEffectsService.PlayChime();

        _toastTimer.Stop();
        _toastTimer.Start();
    }
    #endregion

    #region Expand / Collapse Animations
    private void IslandContainer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        double w = Math.Max(0, IslandContainer.ActualWidth > 0 ? IslandContainer.ActualWidth : ActualWidth);
        double h = Math.Max(0, IslandContainer.ActualHeight > 0 ? IslandContainer.ActualHeight : ActualHeight);
        if (_isAvatarMiniMode)
        {
            double r = Math.Min(w, h) / 2.0;
            IslandClipGeometry.Rect = new Rect(0, 0, Math.Max(0, w - 2.6), Math.Max(0, h - 2.6));
            IslandClipGeometry.RadiusX = Math.Max(0, r - 1.3);
            IslandClipGeometry.RadiusY = Math.Max(0, r - 1.3);
        }
        else
        {
            double r = IslandContainer.CornerRadius.TopLeft > 0 ? IslandContainer.CornerRadius.TopLeft : IslandContainer.CornerRadius.BottomLeft;
            r = Math.Max(2, r);
            IslandClipGeometry.Rect = new Rect(0, 0, Math.Max(0, w - 2.6), Math.Max(0, h - 2.6));
            IslandClipGeometry.RadiusX = Math.Max(0, r - 1.3);
            IslandClipGeometry.RadiusY = Math.Max(0, r - 1.3);
        }
    }

    private void IslandContainer_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_isSleepingForFullscreen || _isAvatarMiniMode) return;
        if ((DateTime.Now - _lastExitMiniModeTime).TotalMilliseconds < 800) return;

        _hoverCollapseTimer.Stop();
        // Hover auto-expand disabled per user request: island only expands on explicit left click
    }

    private void IslandContainer_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _hoverExpandTimer.Stop();
        if (_isAvatarMiniMode) return;
        if (_isExpanded && !_isPinned)
        {
            _hoverCollapseTimer.Start();
        }
    }

    public void ExpandIsland(int? targetTabIndex = null)
    {
        if (_isAvatarMiniMode) return;

        if (targetTabIndex.HasValue)
        {
            SwitchTab(targetTabIndex.Value);
        }

        if (_isExpanded) return;
        _isExpanded = true;
        _isAnimating = true;

        // Hide overlay banners immediately so expanded views are unobstructed
        ToastBannerOverlay.Visibility = Visibility.Collapsed;
        _toastTimer.Stop();

        var animDuration = TimeSpan.FromMilliseconds(380);
        var ease = new QuarticEase { EasingMode = EasingMode.EaseOut };

        var widthAnim = new DoubleAnimation(688, animDuration) { EasingFunction = ease };
        var heightAnim = new DoubleAnimation(212, animDuration) { EasingFunction = ease };
        var cornerAnim = new CornerRadiusAnimation(IslandContainer.CornerRadius, new CornerRadius(34), animDuration) { EasingFunction = ease };

        Timeline.SetDesiredFrameRate(widthAnim, 144);
        Timeline.SetDesiredFrameRate(heightAnim, 144);
        Timeline.SetDesiredFrameRate(cornerAnim, 144);

        widthAnim.Completed += (s, e) =>
        {
            _isAnimating = false;
            IslandClipGeometry.Rect = new Rect(0, 0, 685.4, 209.4);
            IslandClipGeometry.RadiusX = 32.7;
            IslandClipGeometry.RadiusY = 32.7;
        };

        IslandContainer.BeginAnimation(WidthProperty, widthAnim);
        IslandContainer.BeginAnimation(HeightProperty, heightAnim);
        IslandContainer.BeginAnimation(Border.CornerRadiusProperty, cornerAnim);

        var fadeOut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(130));
        Timeline.SetDesiredFrameRate(fadeOut, 144);
        fadeOut.Completed += (s, e) =>
        {
            if (_isExpanded)
            {
                CollapsedView.Visibility = Visibility.Collapsed;
            }
        };
        CollapsedView.BeginAnimation(OpacityProperty, fadeOut);

        ExpandedView.Visibility = Visibility.Visible;
        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(320))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        var scaleXAnim = new DoubleAnimation(0.94, 1.0, TimeSpan.FromMilliseconds(360)) { EasingFunction = ease };
        var scaleYAnim = new DoubleAnimation(0.94, 1.0, TimeSpan.FromMilliseconds(360)) { EasingFunction = ease };
        var transAnim = new DoubleAnimation(-6, 0, TimeSpan.FromMilliseconds(360)) { EasingFunction = ease };

        Timeline.SetDesiredFrameRate(fadeIn, 144);
        Timeline.SetDesiredFrameRate(scaleXAnim, 144);
        Timeline.SetDesiredFrameRate(scaleYAnim, 144);
        Timeline.SetDesiredFrameRate(transAnim, 144);

        TriggerAvatarReaction(AvatarState.Curious, 3);

        ExpandedView.BeginAnimation(OpacityProperty, fadeIn);
        ExpandedScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXAnim);
        ExpandedScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYAnim);
        ExpandedTranslate.BeginAnimation(TranslateTransform.YProperty, transAnim);
    }

    public void CollapseIsland()
    {
        if (!_isExpanded) return;
        _isExpanded = false;
        _isAnimating = true;
        SpotlightOverlay.Visibility = Visibility.Collapsed;
        LyricsOverlay.Visibility = Visibility.Collapsed;
        SnapOverlay.Visibility = Visibility.Collapsed;
        AvatarOverlay.Visibility = Visibility.Collapsed;

        RestoreDefaultAvatarState();

        var animDuration = TimeSpan.FromMilliseconds(360);
        var ease = new QuarticEase { EasingMode = EasingMode.EaseOut };

        double targetW = _isStealthBarMode ? 280 : 356;
        double targetH = _isStealthBarMode ? 6 : 42;
        var targetCorner = _isStealthBarMode ? new CornerRadius(3) : new CornerRadius(21);

        var widthAnim = new DoubleAnimation(targetW, animDuration) { EasingFunction = ease };
        var heightAnim = new DoubleAnimation(targetH, animDuration) { EasingFunction = ease };
        var cornerAnim = new CornerRadiusAnimation(IslandContainer.CornerRadius, targetCorner, animDuration) { EasingFunction = ease };

        Timeline.SetDesiredFrameRate(widthAnim, 144);
        Timeline.SetDesiredFrameRate(heightAnim, 144);
        Timeline.SetDesiredFrameRate(cornerAnim, 144);

        widthAnim.Completed += (s, e) =>
        {
            _isAnimating = false;
            IslandClipGeometry.Rect = new Rect(0, 0, 353.4, 39.4);
            IslandClipGeometry.RadiusX = 19.8;
            IslandClipGeometry.RadiusY = 19.8;
        };

        IslandContainer.BeginAnimation(WidthProperty, widthAnim);
        IslandContainer.BeginAnimation(HeightProperty, heightAnim);
        IslandContainer.BeginAnimation(Border.CornerRadiusProperty, cornerAnim);

        var fadeOut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(150));
        var scaleAnim = new DoubleAnimation(0.94, TimeSpan.FromMilliseconds(190)) { EasingFunction = ease };
        var transAnim = new DoubleAnimation(-5, TimeSpan.FromMilliseconds(190)) { EasingFunction = ease };

        Timeline.SetDesiredFrameRate(fadeOut, 144);
        Timeline.SetDesiredFrameRate(scaleAnim, 144);
        Timeline.SetDesiredFrameRate(transAnim, 144);

        fadeOut.Completed += (s, e) =>
        {
            if (!_isExpanded)
            {
                ExpandedView.Visibility = Visibility.Collapsed;
                if (_isStealthBarMode)
                {
                    CollapsedView.Visibility = Visibility.Collapsed;
                }
                else
                {
                    CollapsedView.Visibility = Visibility.Visible;
                    var fadeIn = new DoubleAnimation(1, TimeSpan.FromMilliseconds(200))
                    {
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    };
                    Timeline.SetDesiredFrameRate(fadeIn, 144);
                    CollapsedView.BeginAnimation(OpacityProperty, fadeIn);
                }
            }
        };

        ExpandedView.BeginAnimation(OpacityProperty, fadeOut);
        ExpandedScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
        ExpandedScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
        ExpandedTranslate.BeginAnimation(TranslateTransform.YProperty, transAnim);
    }

    private void ApplyCollapsedDimensionsImmediate()
    {
        if (_isStealthBarMode)
        {
            IslandContainer.BeginAnimation(WidthProperty, null);
            IslandContainer.BeginAnimation(HeightProperty, null);
            IslandContainer.BeginAnimation(Border.CornerRadiusProperty, null);
            IslandContainer.Width = 280;
            IslandContainer.Height = 6;
            IslandContainer.CornerRadius = new CornerRadius(3);
            IslandClipGeometry.Rect = new Rect(0, 0, 277.4, 4);
            IslandClipGeometry.RadiusX = 2;
            IslandClipGeometry.RadiusY = 2;
            CollapsedView.Visibility = Visibility.Collapsed;
        }
        else
        {
            IslandContainer.BeginAnimation(WidthProperty, null);
            IslandContainer.BeginAnimation(HeightProperty, null);
            IslandContainer.BeginAnimation(Border.CornerRadiusProperty, null);
            IslandContainer.Width = 356;
            IslandContainer.Height = 42;
            IslandContainer.CornerRadius = new CornerRadius(21);
            IslandClipGeometry.Rect = new Rect(0, 0, 353.4, 39.4);
            IslandClipGeometry.RadiusX = 19.8;
            IslandClipGeometry.RadiusY = 19.8;
            CollapsedView.Visibility = Visibility.Visible;
            CollapsedView.Opacity = 1;
        }
    }

    private void StealthModeBtn_Click(object sender, RoutedEventArgs e)
    {
        _isStealthBarMode = !_isStealthBarMode;
        _savedState.IsStealthBarMode = _isStealthBarMode;
        StorageService.Save(_savedState);
        UpdateStealthModeVisual();

        if (_isStealthBarMode)
        {
            _isPinned = false;
            UpdatePinButtonVisual();
            CollapseIsland();
        }
    }

    private void UpdateStealthModeVisual()
    {
        var lighter = Color.FromRgb(
            (byte)Math.Min(255, _currentAccentColor.R + 45),
            (byte)Math.Min(255, _currentAccentColor.G + 45),
            (byte)Math.Min(255, _currentAccentColor.B + 45));

        StealthModeIconText.Text = _isStealthBarMode ? "➖ Açık" : "➖";
        StealthModeBtn.Background = _isStealthBarMode
            ? new SolidColorBrush(Color.FromArgb(65, _currentAccentColor.R, _currentAccentColor.G, _currentAccentColor.B))
            : new SolidColorBrush(Color.FromArgb(204, 13, 16, 29));
        StealthModeBtn.BorderBrush = _isStealthBarMode
            ? new SolidColorBrush(Color.FromArgb(190, lighter.R, lighter.G, lighter.B))
            : new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
    }

    private void QuickShiftPosition_Click(object sender, RoutedEventArgs e)
    {
        double screenW = SystemParameters.PrimaryScreenWidth;
        double centerLeft = (screenW - Width) / 2.0;
        // Browser right-side titlebar gap (around 72% across the screen, leaving room for min/max/close buttons)
        double rightGapLeft = Math.Min(screenW - Width - 140, centerLeft + 340);

        bool isCurrentlyCentered = Math.Abs(Left - centerLeft) < 60;
        double targetLeft = isCurrentlyCentered ? rightGapLeft : centerLeft;

        Left = targetLeft;
        _savedState.CustomLeftX = isCurrentlyCentered ? targetLeft : null;
        StorageService.Save(_savedState);
    }

    private void IslandContainer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_isAvatarMiniMode)
        {
            _miniLongPressTriggered = false;
            _miniHoldTimer.Stop();
            _miniHoldTimer.Start();
            e.Handled = true;
            return;
        }

        // Only start dragging if user clicked on the island background/header, not a Button, Slider, or TextBox
        if (e.OriginalSource is DependencyObject dep)
        {
            if (dep.FindVisualParent<Button>() != null ||
                dep.FindVisualParent<Slider>() != null ||
                dep.FindVisualParent<TextBox>() != null)
            {
                return;
            }
        }

        _isDraggingIsland = true;
        var pos = Forms.Control.MousePosition;
        _dragStartScreenPoint = new System.Windows.Point(pos.X, pos.Y);
        _dragStartWindowLeft = Left;
        IslandContainer.CaptureMouse();
    }

    private void IslandContainer_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isDraggingIsland) return;

        var pos = Forms.Control.MousePosition;
        double deltaX = pos.X - _dragStartScreenPoint.X;
        if (Math.Abs(deltaX) > 2)
        {
            double screenW = SystemParameters.PrimaryScreenWidth;
            Left = Math.Clamp(_dragStartWindowLeft + deltaX, -120, screenW - Width + 120);
        }
    }

    private void IslandContainer_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isAvatarMiniMode)
        {
            _miniHoldTimer.Stop();
            if (_miniLongPressTriggered)
            {
                e.Handled = true;
                return;
            }

            if (AvatarMiniRadialCanvas.Visibility == Visibility.Visible)
            {
                HideMiniRadialControls();
                e.Handled = true;
                return;
            }

            ExitAvatarMiniMode();
            e.Handled = true;
            return;
        }

        if ((DateTime.Now - _lastExitMiniModeTime).TotalMilliseconds < 800)
        {
            e.Handled = true;
            return;
        }

        if (!_isDraggingIsland)
        {
            if (!_isExpanded)
            {
                ExpandIsland(_activeTabIndex);
            }
            return;
        }

        _isDraggingIsland = false;
        IslandContainer.ReleaseMouseCapture();

        if (Math.Abs(Left - _dragStartWindowLeft) > 4)
        {
            _savedState.CustomLeftX = Left;
            StorageService.Save(_savedState);
        }
        else
        {
            // Clicked without dragging: expand island!
            if (!_isExpanded)
            {
                ExpandIsland(_activeTabIndex);
            }
        }
    }

    private void IslandContainer_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Suppress Windows context menu immediately
        e.Handled = true;
    }

    private void IslandContainer_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (!SettingsService.Current.MiniMode.MiniModeEnabled || !SettingsService.Current.MiniMode.RightClickMiniMode)
        {
            return;
        }

        if (_isAvatarMiniMode)
        {
            ExitAvatarMiniMode();
        }
        else
        {
            EnterAvatarMiniMode();
        }
    }

    #region Avatar Mini Mode (Top-Right Floating Bubble & Interactive Dynamics)
    private static double EaseInOutCubic(double t)
    {
        return t < 0.5 ? 4.0 * t * t * t : 1.0 - Math.Pow(-2.0 * t + 2.0, 3.0) / 2.0;
    }

    public void EnterAvatarMiniMode(bool immediate = false)
    {
        if (_isAvatarMiniMode || !SettingsService.Current.MiniMode.MiniModeEnabled) return;

        // If expanded, close overlays and collapse
        if (_isExpanded)
        {
            SpotlightOverlay.Visibility = Visibility.Collapsed;
            LyricsOverlay.Visibility = Visibility.Collapsed;
            SnapOverlay.Visibility = Visibility.Collapsed;
            AvatarOverlay.Visibility = Visibility.Collapsed;
            _isExpanded = false;
            ExpandedView.Visibility = Visibility.Collapsed;
        }

        _isAvatarMiniMode = true;
        _savedState.IsInAvatarMiniMode = true;
        StorageService.Save(_savedState);

        double screenW = SystemParameters.PrimaryScreenWidth;

        // Cache pre-mini window position to accurately restore on return
        if (!immediate)
        {
            _preMiniLeft = Left;
            _preMiniTop = Top;
            _isAnimating = true;

            // Sound effect & avatar cute reaction
            SoundEffectsService.PlayCollapse();
            TriggerAvatarReaction(AvatarState.Curious, 3);
        }
        else
        {
            if (_savedState.CustomLeftX.HasValue)
            {
                _preMiniLeft = Math.Clamp(_savedState.CustomLeftX.Value, -100, screenW - Width + 100);
            }
            else
            {
                _preMiniLeft = (screenW - (ActualWidth > 0 ? ActualWidth : Width)) / 2.0;
            }
            _preMiniTop = 0;
            _isAnimating = false;
        }

        // Hide overlay banners immediately
        ToastBannerOverlay.Visibility = Visibility.Collapsed;
        QuickVolOverlay.Visibility = Visibility.Collapsed;
        _toastTimer.Stop();
        _quickVolTimer.Stop();
        HideMiniRadialControls();
        HideMiniSpeechBubble();

        if (NotchLeftEar != null) NotchLeftEar.Visibility = Visibility.Collapsed;
        if (NotchRightEar != null) NotchRightEar.Visibility = Visibility.Collapsed;
        if (NotchLeftEarRim != null) NotchLeftEarRim.Visibility = Visibility.Collapsed;
        if (NotchRightEarRim != null) NotchRightEarRim.Visibility = Visibility.Collapsed;
        if (NotchVolumetricGlow != null) NotchVolumetricGlow.Visibility = Visibility.Collapsed;
        if (NotchRainCanvas != null) NotchRainCanvas.Visibility = Visibility.Collapsed;

        IslandContainer.ToolTip = "Onee Avatar Mini Mod • Normal ada moduna dönmek için tıklayın";

        // Calculate target position in top-right or top-left corner of screen
        double bubbleSize = Math.Clamp(SettingsService.Current.MiniMode.BubbleSize, 48.0, 76.0);
        double windowHalfW = (ActualWidth > 0 ? ActualWidth : Width) / 2.0;
        double targetLeft;
        if (SettingsService.Current.MiniMode.BubblePosition == "Top Left")
        {
            double bubbleCenterX = 24.0 + (bubbleSize / 2.0);
            targetLeft = bubbleCenterX - windowHalfW;
        }
        else
        {
            double bubbleCenterX = screenW - 24.0 - (bubbleSize / 2.0);
            targetLeft = bubbleCenterX - windowHalfW;
        }
        double targetTop = 16.0;

        if (immediate)
        {
            Left = targetLeft;
            Top = targetTop;
            IslandContainer.Width = bubbleSize;
            IslandContainer.Height = bubbleSize;
            IslandContainer.CornerRadius = new CornerRadius(bubbleSize / 2.0);
            IslandClipGeometry.Rect = new Rect(0, 0, bubbleSize, bubbleSize);
            IslandClipGeometry.RadiusX = bubbleSize / 2.0;
            IslandClipGeometry.RadiusY = bubbleSize / 2.0;
            AvatarMiniModeView.Width = bubbleSize;
            AvatarMiniModeView.Height = bubbleSize;
            CollapsedView.Visibility = Visibility.Collapsed;
            AvatarMiniModeView.Visibility = Visibility.Visible;
            AvatarMiniModeView.Opacity = 1.0;
            _lastAppliedAmbientColor = Colors.Transparent;
            CheckAndUpdateAmbientColor();
            return;
        }

        double startLeft = Left;
        double startTop = Top;

        double durationMs = SettingsService.Current.MiniMode.TransitionSpeed switch
        {
            "Slow" => 550,
            "Fast" => 220,
            _ => 380
        };
        if (SettingsService.Current.Performance.ReduceMotion) durationMs = 100;
        var animDuration = TimeSpan.FromMilliseconds(durationMs);
        var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };

        // 1. Morph Dimensions: 356x42 -> bubbleSizexbubbleSize with CornerRadius 21 -> bubbleSize/2 (circle)
        var widthAnim = new DoubleAnimation(IslandContainer.ActualWidth, bubbleSize, animDuration) { EasingFunction = ease };
        var heightAnim = new DoubleAnimation(IslandContainer.ActualHeight, bubbleSize, animDuration) { EasingFunction = ease };
        var cornerAnim = new CornerRadiusAnimation(IslandContainer.CornerRadius, new CornerRadius(bubbleSize / 2.0), animDuration) { EasingFunction = ease };

        Timeline.SetDesiredFrameRate(widthAnim, 144);
        Timeline.SetDesiredFrameRate(heightAnim, 144);
        Timeline.SetDesiredFrameRate(cornerAnim, 144);

        widthAnim.Completed += (s, e) =>
        {
            _isAnimating = false;
            IslandContainer.Width = bubbleSize;
            IslandContainer.Height = bubbleSize;
            IslandContainer.CornerRadius = new CornerRadius(bubbleSize / 2.0);
            IslandClipGeometry.Rect = new Rect(0, 0, bubbleSize, bubbleSize);
            IslandClipGeometry.RadiusX = bubbleSize / 2.0;
            IslandClipGeometry.RadiusY = bubbleSize / 2.0;
            AvatarMiniModeView.Width = bubbleSize;
            AvatarMiniModeView.Height = bubbleSize;

            if (_isMediaActive && _isMediaPlaying && _lastAnalyzedCover != null)
            {
                Color songColor = AdaptiveColorService.ExtractDominantColor(_lastAnalyzedCover);
                ApplyAdaptiveAccentColor(songColor);
            }
            else
            {
                _lastAppliedAmbientColor = Colors.Transparent;
                CheckAndUpdateAmbientColor();
            }
        };

        IslandContainer.BeginAnimation(WidthProperty, widthAnim);
        IslandContainer.BeginAnimation(HeightProperty, heightAnim);
        IslandContainer.BeginAnimation(Border.CornerRadiusProperty, cornerAnim);

        // 2. Squash & Stretch Physics Keyframes (Apple fluid morphing feel)
        if (SettingsService.Current.MiniMode.FluidMorph && !SettingsService.Current.Performance.ReduceMotion)
        {
            var squashX = new DoubleAnimationUsingKeyFrames { Duration = animDuration };
            squashX.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromPercent(0.0)));
            squashX.KeyFrames.Add(new SplineDoubleKeyFrame(0.86, KeyTime.FromPercent(0.35))); // horizontally compressed during travel
            squashX.KeyFrames.Add(new SplineDoubleKeyFrame(1.08, KeyTime.FromPercent(0.80))); // overshoot on arrival
            squashX.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromPercent(1.0)));   // settle
            Timeline.SetDesiredFrameRate(squashX, 144);

            var stretchY = new DoubleAnimationUsingKeyFrames { Duration = animDuration };
            stretchY.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromPercent(0.0)));
            stretchY.KeyFrames.Add(new SplineDoubleKeyFrame(1.07, KeyTime.FromPercent(0.35))); // slightly stretched vertically
            stretchY.KeyFrames.Add(new SplineDoubleKeyFrame(0.93, KeyTime.FromPercent(0.80))); // squash on bounce
            stretchY.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromPercent(1.0)));   // settle
            Timeline.SetDesiredFrameRate(stretchY, 144);

            IslandContainerScale.BeginAnimation(ScaleTransform.ScaleXProperty, squashX);
            IslandContainerScale.BeginAnimation(ScaleTransform.ScaleYProperty, stretchY);
        }
        else
        {
            IslandContainerScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            IslandContainerScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        }

        // 3. Seamless Content Crossfade
        var fadeOutCollapsed = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(130));
        Timeline.SetDesiredFrameRate(fadeOutCollapsed, 144);
        fadeOutCollapsed.Completed += (s, e) =>
        {
            if (_isAvatarMiniMode)
            {
                CollapsedView.Visibility = Visibility.Collapsed;
            }
        };
        CollapsedView.BeginAnimation(OpacityProperty, fadeOutCollapsed);

        AvatarMiniModeView.Visibility = Visibility.Visible;
        AvatarMiniModeView.BeginAnimation(OpacityProperty, null);
        AvatarMiniModeView.Opacity = 0.0;
        var fadeInMini = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(220))
        {
            BeginTime = TimeSpan.FromMilliseconds(90),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Timeline.SetDesiredFrameRate(fadeInMini, 144);
        AvatarMiniModeView.BeginAnimation(OpacityProperty, fadeInMini);

        // 4. Smooth 144Hz window trajectory to top-right corner
        if (_currentWindowMoveHandler != null)
        {
            CompositionTarget.Rendering -= _currentWindowMoveHandler;
            _currentWindowMoveHandler = null;
        }

        var sw = Stopwatch.StartNew();
        double totalDurationMs = 380.0;

        void MoveTick(object? s, EventArgs e)
        {
            double t = Math.Clamp(sw.Elapsed.TotalMilliseconds / totalDurationMs, 0.0, 1.0);
            double eased = EaseInOutCubic(t);
            Left = startLeft + (targetLeft - startLeft) * eased;
            Top = startTop + (targetTop - startTop) * eased;

            if (t >= 1.0)
            {
                CompositionTarget.Rendering -= MoveTick;
                _currentWindowMoveHandler = null;
                sw.Stop();
                Left = targetLeft;
                Top = targetTop;
                _isAnimating = false;

                IslandContainer.Width = bubbleSize;
                IslandContainer.Height = bubbleSize;
                IslandContainer.CornerRadius = new CornerRadius(bubbleSize / 2.0);
                IslandClipGeometry.Rect = new Rect(0, 0, bubbleSize, bubbleSize);
                IslandClipGeometry.RadiusX = bubbleSize / 2.0;
                IslandClipGeometry.RadiusY = bubbleSize / 2.0;
                AvatarMiniModeView.Width = bubbleSize;
                AvatarMiniModeView.Height = bubbleSize;

                if (_isMediaActive && _isMediaPlaying && _lastAnalyzedCover != null)
                {
                    Color songColor = AdaptiveColorService.ExtractDominantColor(_lastAnalyzedCover);
                    ApplyAdaptiveAccentColor(songColor);
                }
                else
                {
                    _lastAppliedAmbientColor = Colors.Transparent;
                    CheckAndUpdateAmbientColor();
                }
            }
        }

        _currentWindowMoveHandler = MoveTick;
        CompositionTarget.Rendering += MoveTick;
    }

    public void ExitAvatarMiniMode()
    {
        if (!_isAvatarMiniMode) return;
        _isAvatarMiniMode = false;
        _isAnimating = true;
        _lastExitMiniModeTime = DateTime.Now;
        _savedState.IsInAvatarMiniMode = false;
        StorageService.Save(_savedState);

        SoundEffectsService.PlayExpand();
        TriggerAvatarReaction(AvatarState.Happy, 3);

        IslandContainer.ToolTip = "İpucu: Sağa/Sola sürükleyebilirsiniz • Sağ Tık: Avatar Mini Mod";

        double startLeft = Left;
        double startTop = Top;
        double screenW = SystemParameters.PrimaryScreenWidth;
        double targetLeft = _savedState.CustomLeftX.HasValue
            ? Math.Clamp(_savedState.CustomLeftX.Value, -100, screenW - Width + 100)
            : (screenW - Width) / 2.0;
        double targetTop = 0;

        // 1. Immediately switch back to normal island views cleanly and remove ANY animation clocks
        var fadeOutMini = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(120));
        Timeline.SetDesiredFrameRate(fadeOutMini, 144);
        fadeOutMini.Completed += (s, e) =>
        {
            if (!_isAvatarMiniMode)
            {
                AvatarMiniModeView.Visibility = Visibility.Collapsed;
            }
        };
        AvatarMiniModeView.BeginAnimation(OpacityProperty, fadeOutMini);
        HideMiniRadialControls();
        HideMiniSpeechBubble();

        CollapsedView.BeginAnimation(OpacityProperty, null);
        CollapsedCenterGrid.BeginAnimation(OpacityProperty, null);
        CollapsedRightPanel.BeginAnimation(OpacityProperty, null);
        CollapsedLeftPanel.BeginAnimation(OpacityProperty, null);

        CollapsedView.Visibility = Visibility.Visible;
        CollapsedView.Opacity = 0.0;
        CollapsedCenterGrid.Visibility = Visibility.Visible;
        CollapsedCenterGrid.Opacity = 1.0;
        CollapsedRightPanel.Visibility = Visibility.Visible;
        CollapsedRightPanel.Opacity = 1.0;
        CollapsedLeftPanel.Visibility = Visibility.Visible;
        CollapsedLeftPanel.Opacity = 1.0;

        // 2. Immediately expand clip geometry so nothing inside is clipped during window resizing
        IslandClipGeometry.Rect = new Rect(0, 0, 353.4, 39.4);
        IslandClipGeometry.RadiusX = 19.8;
        IslandClipGeometry.RadiusY = 19.8;

        // 3. Immediately populate text and indicators synchronously so the pill is NEVER blank
        var now = DateTime.Now;
        string timeStr = now.ToString("HH:mm");
        string dayStr = now.ToString("dddd");

        if (_isMediaActive && !string.IsNullOrWhiteSpace(MiniTrackTitleText.Text))
        {
            CollapsedCenterGrid.Visibility = Visibility.Collapsed;
            MiniMediaInfoPanel.Visibility = Visibility.Visible;
            MiniCenterText.Visibility = Visibility.Collapsed;
            MiniThumbBorder.Visibility = _lastAnalyzedCover != null ? Visibility.Visible : Visibility.Collapsed;
            MiniStatusDot.Visibility = _lastAnalyzedCover != null ? Visibility.Collapsed : Visibility.Visible;
            EqualizerPanel.Visibility = Visibility.Visible;
        }
        else
        {
            MiniMediaInfoPanel.Visibility = Visibility.Collapsed;
            CollapsedCenterGrid.Visibility = Visibility.Visible;
            MiniCenterText.Visibility = Visibility.Visible;
            MiniCenterText.Text = $"{timeStr}  •  {dayStr}";
            MiniThumbBorder.Visibility = Visibility.Collapsed;
            MiniStatusDot.Visibility = Visibility.Visible;
            EqualizerPanel.Visibility = Visibility.Collapsed;
        }

        MiniBatteryBorder.Visibility = Visibility.Visible;
        MiniAvatarHostBorder.Visibility = Visibility.Visible;
        MiniAvatarHostBorder.Opacity = 1.0;

        CollapsedView.InvalidateVisual();
        IslandContainer.InvalidateVisual();

        // Crossfade CollapsedView in smoothly
        var fadeInCollapsed = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(260))
        {
            BeginTime = TimeSpan.FromMilliseconds(80),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Timeline.SetDesiredFrameRate(fadeInCollapsed, 144);
        CollapsedView.BeginAnimation(OpacityProperty, fadeInCollapsed);

        double durationMs = SettingsService.Current.MiniMode.TransitionSpeed switch
        {
            "Slow" => 550,
            "Fast" => 220,
            _ => 380
        };
        if (SettingsService.Current.Performance.ReduceMotion) durationMs = 100;
        var animDuration = TimeSpan.FromMilliseconds(durationMs);
        var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };

        // 4. Morph dimensions: bubbleSize -> 356x42, CornerRadius bubbleSize/2 -> 21
        var widthAnim = new DoubleAnimation(IslandContainer.ActualWidth, 356, animDuration) { EasingFunction = ease };
        var heightAnim = new DoubleAnimation(IslandContainer.ActualHeight, 42, animDuration) { EasingFunction = ease };
        var cornerAnim = new CornerRadiusAnimation(IslandContainer.CornerRadius, new CornerRadius(21), animDuration) { EasingFunction = ease };

        Timeline.SetDesiredFrameRate(widthAnim, 144);
        Timeline.SetDesiredFrameRate(heightAnim, 144);
        Timeline.SetDesiredFrameRate(cornerAnim, 144);

        widthAnim.Completed += (s, e) =>
        {
            _isAnimating = false;
            IslandContainer.Width = 356;
            IslandContainer.Height = 42;
            IslandContainer.CornerRadius = new CornerRadius(21);
            IslandClipGeometry.Rect = new Rect(0, 0, 353.4, 39.4);
            IslandClipGeometry.RadiusX = 19.8;
            IslandClipGeometry.RadiusY = 19.8;

            if (NotchLeftEar != null) NotchLeftEar.Visibility = Visibility.Collapsed;
            if (NotchRightEar != null) NotchRightEar.Visibility = Visibility.Collapsed;
            if (NotchLeftEarRim != null) NotchLeftEarRim.Visibility = Visibility.Collapsed;
            if (NotchRightEarRim != null) NotchRightEarRim.Visibility = Visibility.Collapsed;
            if (NotchVolumetricGlow != null) NotchVolumetricGlow.Visibility = Visibility.Collapsed;

            CollapsedView.UpdateLayout();
            IslandContainer.UpdateLayout();
            _ = MainUpdateLoopAsync();
            _lastAppliedAmbientColor = Colors.Transparent;
            CheckAndUpdateAmbientColor();
        };

        IslandContainer.BeginAnimation(WidthProperty, widthAnim);
        IslandContainer.BeginAnimation(HeightProperty, heightAnim);
        IslandContainer.BeginAnimation(Border.CornerRadiusProperty, cornerAnim);

        // 5. Anticipation and elastic rebound keyframes
        if (SettingsService.Current.MiniMode.FluidMorph && !SettingsService.Current.Performance.ReduceMotion)
        {
            var expandX = new DoubleAnimationUsingKeyFrames { Duration = animDuration };
            expandX.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromPercent(0.0)));
            expandX.KeyFrames.Add(new SplineDoubleKeyFrame(1.08, KeyTime.FromPercent(0.18))); // pop up
            expandX.KeyFrames.Add(new SplineDoubleKeyFrame(0.96, KeyTime.FromPercent(0.70))); // compression
            expandX.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromPercent(1.0)));   // settle
            Timeline.SetDesiredFrameRate(expandX, 144);

            var expandY = new DoubleAnimationUsingKeyFrames { Duration = animDuration };
            expandY.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromPercent(0.0)));
            expandY.KeyFrames.Add(new SplineDoubleKeyFrame(0.93, KeyTime.FromPercent(0.18))); // squash
            expandY.KeyFrames.Add(new SplineDoubleKeyFrame(1.04, KeyTime.FromPercent(0.70))); // rebound
            expandY.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromPercent(1.0)));   // settle
            Timeline.SetDesiredFrameRate(expandY, 144);

            IslandContainerScale.BeginAnimation(ScaleTransform.ScaleXProperty, expandX);
            IslandContainerScale.BeginAnimation(ScaleTransform.ScaleYProperty, expandY);
        }
        else
        {
            IslandContainerScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            IslandContainerScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        }

        // 6. Smooth window return animation
        if (_currentWindowMoveHandler != null)
        {
            CompositionTarget.Rendering -= _currentWindowMoveHandler;
            _currentWindowMoveHandler = null;
        }

        var sw = Stopwatch.StartNew();
        double totalDurationMs = durationMs;

        void ReturnMoveTick(object? s, EventArgs e)
        {
            double t = Math.Clamp(sw.Elapsed.TotalMilliseconds / totalDurationMs, 0.0, 1.0);
            double eased = EaseInOutCubic(t);
            Left = startLeft + (targetLeft - startLeft) * eased;
            Top = startTop + (targetTop - startTop) * eased;

            if (t >= 1.0)
            {
                CompositionTarget.Rendering -= ReturnMoveTick;
                _currentWindowMoveHandler = null;
                sw.Stop();
                Left = targetLeft;
                Top = targetTop;
                _isAnimating = false;

                _lastAppliedAmbientColor = Colors.Transparent;
                CheckAndUpdateAmbientColor();
            }
        }

        _currentWindowMoveHandler = ReturnMoveTick;
        CompositionTarget.Rendering += ReturnMoveTick;
    }

    private void AvatarMiniMode_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        _miniHoldTimer.Stop();
        if (_miniLongPressTriggered)
        {
            return;
        }

        if (AvatarMiniRadialCanvas != null && AvatarMiniRadialCanvas.Visibility == Visibility.Visible)
        {
            HideMiniRadialControls();
            return;
        }

        ExitAvatarMiniMode();
    }

    private void AvatarMiniMode_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        CycleAvatarMood();
    }

    private void AvatarMiniMode_MouseEnter(object sender, MouseEventArgs e)
    {
        if (!_isAvatarMiniMode) return;

        // Interactive Hover Pop to 1.08x with gentle spring bounce
        var popAnim = new DoubleAnimation(1.08, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.32 }
        };
        MiniBubbleHostScale.BeginAnimation(ScaleTransform.ScaleXProperty, popAnim);
        MiniBubbleHostScale.BeginAnimation(ScaleTransform.ScaleYProperty, popAnim);

        SoundEffectsService.PlayTick();
    }

    private void AvatarMiniMode_MouseLeave(object sender, MouseEventArgs e)
    {
        if (!_isAvatarMiniMode) return;

        // Settle scale back to 1.0
        var resetScale = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.2 }
        };
        MiniBubbleHostScale.BeginAnimation(ScaleTransform.ScaleXProperty, resetScale);
        MiniBubbleHostScale.BeginAnimation(ScaleTransform.ScaleYProperty, resetScale);

        // Reset interactive eye tracking smoothly to center
        var resetEye = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        LeftEyeTrackTranslate.BeginAnimation(TranslateTransform.XProperty, resetEye);
        LeftEyeTrackTranslate.BeginAnimation(TranslateTransform.YProperty, resetEye);
        RightEyeTrackTranslate.BeginAnimation(TranslateTransform.XProperty, resetEye);
        RightEyeTrackTranslate.BeginAnimation(TranslateTransform.YProperty, resetEye);
    }

    private void AvatarMiniMode_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isAvatarMiniMode) return;

        // Interactive Eye Tracking: cursor position relative to 58x58 bubble center (29, 29)
        var pos = e.GetPosition(AvatarMiniModeView);
        double dx = pos.X - 29.0;
        double dy = pos.Y - 29.0;
        double dist = Math.Sqrt(dx * dx + dy * dy);
        double maxEyeShift = 3.6;

        double targetX = 0;
        double targetY = 0;
        if (dist > 0.5)
        {
            double factor = Math.Min(dist / 29.0, 1.0);
            targetX = (dx / dist) * maxEyeShift * factor;
            targetY = (dy / dist) * maxEyeShift * factor;
        }

        LeftEyeTrackTranslate.BeginAnimation(TranslateTransform.XProperty, null);
        LeftEyeTrackTranslate.BeginAnimation(TranslateTransform.YProperty, null);
        RightEyeTrackTranslate.BeginAnimation(TranslateTransform.XProperty, null);
        RightEyeTrackTranslate.BeginAnimation(TranslateTransform.YProperty, null);

        LeftEyeTrackTranslate.X = targetX;
        LeftEyeTrackTranslate.Y = targetY;
        RightEyeTrackTranslate.X = targetX;
        RightEyeTrackTranslate.Y = targetY;
    }

    private void UpdateMiniMoodBadge(AvatarState state)
    {
        if (AvatarMiniMoodBadge == null || AvatarMiniMoodBadgeText == null) return;

        string emoji = state switch
        {
            AvatarState.Happy => "✨",
            AvatarState.Excited => "⚡",
            AvatarState.Surprised => "!",
            AvatarState.Curious => "?",
            AvatarState.Playful => "😋",
            AvatarState.Proud => "👑",
            AvatarState.Thinking => "💭",
            AvatarState.Listening => "🎵",
            AvatarState.Sleeping => "zZz",
            AvatarState.Working => "✦",
            AvatarState.Angry => "💢",
            AvatarState.Drowsy => "💤",
            _ => "✨"
        };

        AvatarMiniMoodBadgeText.Text = emoji;
        AvatarMiniMoodBadge.Visibility = Visibility.Visible;

        // Pop & bounce animation
        var fadeIn = new DoubleAnimation(0.95, TimeSpan.FromMilliseconds(160));
        var popScale = new DoubleAnimation(0.5, 1.15, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.4 }
        };
        Timeline.SetDesiredFrameRate(fadeIn, 144);
        Timeline.SetDesiredFrameRate(popScale, 144);

        popScale.Completed += (s, e) =>
        {
            var settle = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(100));
            AvatarMiniMoodBadgeScale.BeginAnimation(ScaleTransform.ScaleXProperty, settle);
            AvatarMiniMoodBadgeScale.BeginAnimation(ScaleTransform.ScaleYProperty, settle);

            var fadeOut = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(400))
            {
                BeginTime = TimeSpan.FromSeconds(2.0)
            };
            fadeOut.Completed += (s2, e2) => AvatarMiniMoodBadge.Visibility = Visibility.Collapsed;
            AvatarMiniMoodBadge.BeginAnimation(OpacityProperty, fadeOut);
        };

        AvatarMiniMoodBadge.BeginAnimation(OpacityProperty, fadeIn);
        AvatarMiniMoodBadgeScale.BeginAnimation(ScaleTransform.ScaleXProperty, popScale);
        AvatarMiniMoodBadgeScale.BeginAnimation(ScaleTransform.ScaleYProperty, popScale);

        // Burst golden aura rays (\ | /) on positive/excited emotions
        if (state == AvatarState.Happy || state == AvatarState.Excited || state == AvatarState.Celebrate || state == AvatarState.Surprised)
        {
            FlashMiniAura();
        }
    }

    private void FlashMiniAura()
    {
        if (AvatarMiniAuraCanvas == null) return;

        var auraIn = new DoubleAnimation(0.85, TimeSpan.FromMilliseconds(150))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        auraIn.Completed += (s, e) =>
        {
            var auraOut = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(500))
            {
                BeginTime = TimeSpan.FromMilliseconds(300),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            AvatarMiniAuraCanvas.BeginAnimation(OpacityProperty, auraOut);
        };
        AvatarMiniAuraCanvas.BeginAnimation(OpacityProperty, auraIn);
    }

    private void DanceTimer_Tick()
    {
        if (!SettingsService.Current.Avatar.IdleAnimation || !SettingsService.Current.Behavior.MusicReactions || !_isMediaActive || !_isMediaPlaying)
        {
            if (NormalIslandAvatarBobTranslate != null && (NormalIslandAvatarBobTranslate.Y != 0 || NormalIslandAvatarBobRotate.Angle != 0))
            {
                NormalIslandAvatarBobTranslate.BeginAnimation(TranslateTransform.YProperty, null);
                NormalIslandAvatarBobTranslate.Y = 0;
                NormalIslandAvatarBobRotate.BeginAnimation(RotateTransform.AngleProperty, null);
                NormalIslandAvatarBobRotate.Angle = 0;
            }
            if (MiniBubbleHostBobTranslate != null && (MiniBubbleHostBobTranslate.Y != 0 || MiniBubbleHostBobRotate.Angle != 0))
            {
                MiniBubbleHostBobTranslate.BeginAnimation(TranslateTransform.YProperty, null);
                MiniBubbleHostBobTranslate.Y = 0;
                MiniBubbleHostBobRotate.BeginAnimation(RotateTransform.AngleProperty, null);
                MiniBubbleHostBobRotate.Angle = 0;
            }
            if (OneeLeftHandTranslate != null && OneeLeftHandTranslate.Y != 0)
            {
                OneeLeftHandTranslate.BeginAnimation(TranslateTransform.YProperty, null);
                OneeLeftHandTranslate.Y = 0;
            }
            if (OneeRightHandTranslate != null && OneeRightHandTranslate.Y != 0)
            {
                OneeRightHandTranslate.BeginAnimation(TranslateTransform.YProperty, null);
                OneeRightHandTranslate.Y = 0;
            }
            if (MiniBubbleLeftHandTranslate != null && MiniBubbleLeftHandTranslate.Y != 0)
            {
                MiniBubbleLeftHandTranslate.BeginAnimation(TranslateTransform.YProperty, null);
                MiniBubbleLeftHandTranslate.Y = 0;
            }
            if (MiniBubbleRightHandTranslate != null && MiniBubbleRightHandTranslate.Y != 0)
            {
                MiniBubbleRightHandTranslate.BeginAnimation(TranslateTransform.YProperty, null);
                MiniBubbleRightHandTranslate.Y = 0;
            }
            if (ExpandedAvatarBobTranslate != null && (ExpandedAvatarBobTranslate.Y != 0 || ExpandedAvatarBobRotate.Angle != 0))
            {
                ExpandedAvatarBobTranslate.BeginAnimation(TranslateTransform.YProperty, null);
                ExpandedAvatarBobTranslate.Y = 0;
                ExpandedAvatarBobRotate.BeginAnimation(RotateTransform.AngleProperty, null);
                ExpandedAvatarBobRotate.Angle = 0;
            }
            if (ExpandedStageLeftHandTranslate != null && ExpandedStageLeftHandTranslate.Y != 0)
            {
                ExpandedStageLeftHandTranslate.BeginAnimation(TranslateTransform.YProperty, null);
                ExpandedStageLeftHandTranslate.Y = 0;
            }
            if (ExpandedStageRightHandTranslate != null && ExpandedStageRightHandTranslate.Y != 0)
            {
                ExpandedStageRightHandTranslate.BeginAnimation(TranslateTransform.YProperty, null);
                ExpandedStageRightHandTranslate.Y = 0;
            }
            if (LeftEarGlowEffect != null && LeftEarGlowEffect.Opacity != 0.4)
            {
                LeftEarGlowEffect.BeginAnimation(DropShadowEffect.OpacityProperty, null);
                LeftEarGlowEffect.Opacity = 0.4;
            }
            if (RightEarGlowEffect != null && RightEarGlowEffect.Opacity != 0.4)
            {
                RightEarGlowEffect.BeginAnimation(DropShadowEffect.OpacityProperty, null);
                RightEarGlowEffect.Opacity = 0.4;
            }
            _danceTimer.Stop();
            return;
        }

        _danceBeatFlip = !_danceBeatFlip;
        double bobY = -2.2;
        double tiltAngle = _danceBeatFlip ? 2.8 : -2.8;

        var bobAnim = new DoubleAnimation(bobY, TimeSpan.FromMilliseconds(210))
        {
            AutoReverse = true,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        var tiltAnim = new DoubleAnimation(tiltAngle, TimeSpan.FromMilliseconds(380))
        {
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };

        if (!_isAvatarMiniMode)
        {
            if (NormalIslandAvatarBobTranslate != null && NormalIslandAvatarBobRotate != null)
            {
                NormalIslandAvatarBobTranslate.BeginAnimation(TranslateTransform.YProperty, bobAnim);
                NormalIslandAvatarBobRotate.BeginAnimation(RotateTransform.AngleProperty, tiltAnim);
            }
            if (_isExpanded && TabMediaView != null && TabMediaView.Visibility == Visibility.Visible)
            {
                if (ExpandedAvatarBobTranslate != null && ExpandedAvatarBobRotate != null)
                {
                    ExpandedAvatarBobTranslate.BeginAnimation(TranslateTransform.YProperty, bobAnim);
                    ExpandedAvatarBobRotate.BeginAnimation(RotateTransform.AngleProperty, tiltAnim);
                }
            }
        }
        else
        {
            if (MiniBubbleHostBobTranslate != null && MiniBubbleHostBobRotate != null)
            {
                MiniBubbleHostBobTranslate.BeginAnimation(TranslateTransform.YProperty, bobAnim);
                MiniBubbleHostBobRotate.BeginAnimation(RotateTransform.AngleProperty, tiltAnim);
            }
        }

        // Coucou Hands dancing to the music tempo
        _handBeat = !_handBeat;
        double handY1 = _handBeat ? -3.5 : 1.5;
        double handY2 = _handBeat ? 1.5 : -3.5;
        var handDur = TimeSpan.FromMilliseconds(220);
        var handEase = new SineEase { EasingMode = EasingMode.EaseInOut };

        OneeLeftHandTranslate?.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(handY1, handDur) { EasingFunction = handEase });
        OneeRightHandTranslate?.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(handY2, handDur) { EasingFunction = handEase });
        MiniBubbleLeftHandTranslate?.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(handY1, handDur) { EasingFunction = handEase });
        MiniBubbleRightHandTranslate?.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(handY2, handDur) { EasingFunction = handEase });
        ExpandedStageLeftHandTranslate?.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(handY1, handDur) { EasingFunction = handEase });
        ExpandedStageRightHandTranslate?.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(handY2, handDur) { EasingFunction = handEase });

        // Audio Reactive Notch Ears Glowing Pulse
        if (LeftEarGlowEffect != null && RightEarGlowEffect != null)
        {
            var earAnim = new DoubleAnimation(_danceBeatFlip ? 0.75 : 0.35, TimeSpan.FromMilliseconds(220))
            {
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            LeftEarGlowEffect.BeginAnimation(DropShadowEffect.OpacityProperty, earAnim);
            RightEarGlowEffect.BeginAnimation(DropShadowEffect.OpacityProperty, earAnim);
        }
    }

    private void SetHeadphonesVisible(bool visible)
    {
        if (!SettingsService.Current.Behavior.MusicReactions) visible = false;
        double targetOpacity = visible ? 1.0 : 0.0;
        var anim = new DoubleAnimation(targetOpacity, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };
        NormalIslandHeadphones?.BeginAnimation(OpacityProperty, anim);
        MiniBubbleHeadphones?.BeginAnimation(OpacityProperty, anim);
        ExpandedStageHeadphones?.BeginAnimation(OpacityProperty, anim);
    }

    public void ShowMiniSpeechBubble(string icon, string text)
    {
        if (!_isAvatarMiniMode) return;
        if (AvatarMiniSpeechBubble == null || SpeechBubbleIcon == null || SpeechBubbleText == null) return;

        SpeechBubbleIcon.Text = icon;
        SpeechBubbleText.Text = text;

        // Measure speech bubble size for accurate vertical centering
        AvatarMiniSpeechBubble.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double bubbleH = AvatarMiniSpeechBubble.DesiredSize.Height > 0 ? AvatarMiniSpeechBubble.DesiredSize.Height : 34.0;

        // Find exact location of IslandContainer relative to the window
        var islandPos = IslandContainer.TranslatePoint(new Point(0, 0), this);
        double islandLeft = islandPos.X;
        double islandTop = islandPos.Y;
        double islandW = IslandContainer.ActualWidth > 0 ? IslandContainer.ActualWidth : 58.0;
        double islandH = IslandContainer.ActualHeight > 0 ? IslandContainer.ActualHeight : 58.0;

        double topMargin = Math.Max(0, islandTop + (islandH - bubbleH) / 2.0);

        bool isLeftPosition = SettingsService.Current.MiniMode.BubblePosition == "Top Left";

        if (isLeftPosition)
        {
            // Position on the right of avatar
            AvatarMiniSpeechBubble.HorizontalAlignment = HorizontalAlignment.Left;
            AvatarMiniSpeechBubble.RenderTransformOrigin = new Point(0.0, 0.5);

            if (SpeechBubbleTail != null)
            {
                SpeechBubbleTail.Points = new PointCollection { new Point(8, 12), new Point(0, 18), new Point(8, 24) };
                SpeechBubbleTail.HorizontalAlignment = HorizontalAlignment.Left;
                SpeechBubbleTail.Margin = new Thickness(-6, 0, 0, 0);
            }

            double leftMargin = Math.Max(0, islandLeft + islandW + 2);
            AvatarMiniSpeechBubble.Margin = new Thickness(leftMargin, topMargin, 0, 0);
        }
        else
        {
            // Position on the left of avatar (Top Right mode) - snug against avatar edge
            AvatarMiniSpeechBubble.HorizontalAlignment = HorizontalAlignment.Right;
            AvatarMiniSpeechBubble.RenderTransformOrigin = new Point(1.0, 0.5);

            if (SpeechBubbleTail != null)
            {
                SpeechBubbleTail.Points = new PointCollection { new Point(0, 12), new Point(8, 18), new Point(0, 24) };
                SpeechBubbleTail.HorizontalAlignment = HorizontalAlignment.Right;
                SpeechBubbleTail.Margin = new Thickness(0, 0, -6, 0);
            }

            double rightMargin = Math.Max(0, this.ActualWidth - islandLeft + 2);
            AvatarMiniSpeechBubble.Margin = new Thickness(0, topMargin, rightMargin, 0);
        }

        AvatarMiniSpeechBubble.Visibility = Visibility.Visible;
        SpeechBubbleTranslate.Y = 0;

        var fadeIn = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(160));
        var popScale = new DoubleAnimation(0.15, 1.0, TimeSpan.FromMilliseconds(240))
        {
            EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.35 }
        };
        Timeline.SetDesiredFrameRate(fadeIn, 144);
        Timeline.SetDesiredFrameRate(popScale, 144);

        AvatarMiniSpeechBubble.BeginAnimation(OpacityProperty, fadeIn);
        SpeechBubbleScale.BeginAnimation(ScaleTransform.ScaleXProperty, popScale);
        SpeechBubbleScale.BeginAnimation(ScaleTransform.ScaleYProperty, popScale);

        _speechBubbleTimer.Stop();
        _speechBubbleTimer.Start();
    }

    private void HideMiniSpeechBubble()
    {
        _speechBubbleTimer.Stop();
        if (AvatarMiniSpeechBubble == null || AvatarMiniSpeechBubble.Visibility != Visibility.Visible) return;

        var fadeOut = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(250))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        var floatUp = new DoubleAnimation(-7.0, TimeSpan.FromMilliseconds(250))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Timeline.SetDesiredFrameRate(fadeOut, 144);
        Timeline.SetDesiredFrameRate(floatUp, 144);

        fadeOut.Completed += (s, e) =>
        {
            AvatarMiniSpeechBubble.Visibility = Visibility.Collapsed;
        };

        AvatarMiniSpeechBubble.BeginAnimation(OpacityProperty, fadeOut);
        SpeechBubbleTranslate.BeginAnimation(TranslateTransform.YProperty, floatUp);
    }

    private void ShowMiniRadialControls()
    {
        if (!_isAvatarMiniMode || AvatarMiniRadialCanvas == null) return;

        RadialPlayPauseIcon.Text = _isMediaPlaying ? "⏸️" : "▶️";

        AvatarMiniRadialCanvas.Visibility = Visibility.Visible;
        var fadeIn = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(180));
        var popScale = new DoubleAnimation(0.4, 1.0, TimeSpan.FromMilliseconds(250))
        {
            EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.4 }
        };
        Timeline.SetDesiredFrameRate(fadeIn, 144);
        Timeline.SetDesiredFrameRate(popScale, 144);

        AvatarMiniRadialCanvas.BeginAnimation(OpacityProperty, fadeIn);
        RadialControlsScale.BeginAnimation(ScaleTransform.ScaleXProperty, popScale);
        RadialControlsScale.BeginAnimation(ScaleTransform.ScaleYProperty, popScale);

        SoundEffectsService.PlayExpand();
    }

    private void HideMiniRadialControls()
    {
        if (AvatarMiniRadialCanvas == null || AvatarMiniRadialCanvas.Visibility != Visibility.Visible) return;

        var fadeOut = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(160));
        var shrinkScale = new DoubleAnimation(0.4, TimeSpan.FromMilliseconds(160))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        Timeline.SetDesiredFrameRate(fadeOut, 144);
        Timeline.SetDesiredFrameRate(shrinkScale, 144);

        fadeOut.Completed += (s, e) =>
        {
            AvatarMiniRadialCanvas.Visibility = Visibility.Collapsed;
        };

        AvatarMiniRadialCanvas.BeginAnimation(OpacityProperty, fadeOut);
        RadialControlsScale.BeginAnimation(ScaleTransform.ScaleXProperty, shrinkScale);
        RadialControlsScale.BeginAnimation(ScaleTransform.ScaleYProperty, shrinkScale);
    }

    private async void MiniRadialPlayPause_Click(object sender, RoutedEventArgs e)
    {
        await _mediaService.PlayPauseAsync();
        RadialPlayPauseIcon.Text = _isMediaPlaying ? "▶️" : "⏸️";
        ShowMiniSpeechBubble(_isMediaPlaying ? "⏸️" : "▶️", _isMediaPlaying ? "Duraklatıldı" : "Oynatılıyor");
        SoundEffectsService.PlayTick();
    }

    private async void MiniRadialNext_Click(object sender, RoutedEventArgs e)
    {
        await _mediaService.NextAsync();
        ShowMiniSpeechBubble("⏭️", "Sonraki Parça");
        SoundEffectsService.PlayTick();
    }

    private void MiniRadialSnip_Click(object sender, RoutedEventArgs e)
    {
        HideMiniRadialControls();
        RegionSnip_Click(sender, e);
    }

    private void MiniRadialVol_Click(object sender, RoutedEventArgs e)
    {
        int nextVol = _masterVolume >= 100 ? 20 : Math.Min(100, _masterVolume + 15);
        AudioService.SetVolume(nextVol);
        _masterVolume = nextVol;
        UpdateVolumeFromSystem();
        ShowMiniSpeechBubble("🔊", $"Ses: %{nextVol}");
        SoundEffectsService.PlayTick();
    }
    #endregion

    private void CollapseButton_Click(object sender, RoutedEventArgs e)
    {
        _isPinned = false;
        UpdatePinButtonVisual();
        CollapseIsland();
    }

    private void PinButton_Click(object sender, RoutedEventArgs e)
    {
        _isPinned = !_isPinned;
        _savedState.IsPinned = _isPinned;
        StorageService.Save(_savedState);
        UpdatePinButtonVisual();
    }

    private void UpdatePinButtonVisual()
    {
        var lighter = Color.FromRgb(
            (byte)Math.Min(255, _currentAccentColor.R + 45),
            (byte)Math.Min(255, _currentAccentColor.G + 45),
            (byte)Math.Min(255, _currentAccentColor.B + 45));

        PinIconText.Text = _isPinned ? "Sabit" : "Sabitle";
        PinButton.Background = _isPinned
            ? new SolidColorBrush(Color.FromArgb(65, _currentAccentColor.R, _currentAccentColor.G, _currentAccentColor.B))
            : new SolidColorBrush(Color.FromArgb(204, 13, 16, 29));
        PinButton.BorderBrush = _isPinned
            ? new SolidColorBrush(Color.FromArgb(190, lighter.R, lighter.G, lighter.B))
            : new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
    }
    #endregion

    #region Screenshot & Snipping Actions
    private async void FullScreenshot_Click(object sender, RoutedEventArgs e)
    {
        await TakeFullScreenshotAsync();
    }

    private async Task TakeFullScreenshotAsync()
    {
        Opacity = 0;
        await Task.Delay(140);
        string? file = ScreenshotService.CaptureFullScreen();
        Opacity = 1;

        if (!string.IsNullOrWhiteSpace(file))
        {
            var item = StorageService.CreateTrayItem(file);
            if (item != null)
            {
                _savedState.TrayFiles.Insert(0, item);
                StorageService.Save(_savedState);
                RefreshFileTrayUi();
                SwitchTab(1);
                ShowToastBanner("📸", "Ekran Görüntüsü", "Tepsiye ve panoya kaydedildi");
            }
        }
    }

    private void RegionSnip_Click(object sender, RoutedEventArgs e)
    {
        try { Clipboard.Clear(); } catch { }
        ScreenshotService.LaunchRegionSnip();
        _snipWatchTicksRemaining = 20; // Watch for ~14 seconds
        _snipWatchTimer.Start();
    }

    private void SnipWatchTimer_Tick(object? sender, EventArgs e)
    {
        _snipWatchTicksRemaining--;
        if (_snipWatchTicksRemaining <= 0)
        {
            _snipWatchTimer.Stop();
            return;
        }

        string? savedFile = ScreenshotService.SaveClipboardImageIfPresent();
        if (!string.IsNullOrWhiteSpace(savedFile))
        {
            _snipWatchTimer.Stop();
            var item = StorageService.CreateTrayItem(savedFile);
            if (item != null)
            {
                _savedState.TrayFiles.Insert(0, item);
                StorageService.Save(_savedState);
                RefreshFileTrayUi();
                SwitchTab(1);
                ShowToastBanner("✂️", "Bölge Kırpıldı", "Kırpılan görsel tepsiye eklendi");
            }
        }
    }
    #endregion

    #region Mouse Wheel Volume Control
    private void IslandContainer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_isAvatarMiniMode) return;

        // If mouse is inside a scrollable text area or list (e.g. notes or tray list), allow scrolling content
        if (sender == QuickNoteTextBox || (e.OriginalSource as DependencyObject)?.FindVisualParent<ScrollViewer>() != null)
        {
            return;
        }

        int delta = e.Delta > 0 ? 3 : -3;
        int nextVol = Math.Clamp(_masterVolume + delta, 0, 100);
        AudioService.SetVolume(nextVol);
        _masterVolume = nextVol;

        if (MasterVolumeSlider != null) MasterVolumeSlider.Value = nextVol;
        if (QuickVolBar != null) QuickVolBar.Value = nextVol;
        if (QuickVolText != null) QuickVolText.Text = $"🔊 Ses: %{nextVol}";

        ShowBezelVolume(nextVol);
        UpdateVolumeFromSystem();
        e.Handled = true;
    }
    #endregion

    #region Navigation Tabs
    private Color GetTabSignatureColor(int tabIndex) => tabIndex switch
    {
        0 => AdaptiveColorService.DefaultAccent,      // Rose #F43F5E (Medya)
        1 => Color.FromRgb(0x63, 0x66, 0xF1),       // Electric Indigo (Tepsi & Pano)
        2 => Color.FromRgb(0x06, 0xB6, 0xD4),       // Cyber Neon Cyan (Sistem & GPU)
        3 => Color.FromRgb(0x10, 0xB9, 0x81),       // Emerald Focus Green (Sayaç & Pomodoro)
        4 => Color.FromRgb(0xA8, 0x55, 0xF7),       // Neon Violet (Hızlı Dock & Ayarlar)
        _ => AdaptiveColorService.DefaultAccent
    };

    private void MiniStatusDot_Click(object sender, MouseButtonEventArgs e)
    {
        int nextTab = (_activeTabIndex + 1) % 5;
        SwitchTab(nextTab);
        SoundEffectsService.PlayExpand();
        e.Handled = true;
    }

    private void TabButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && int.TryParse(btn.Tag?.ToString(), out int tabIndex))
        {
            SwitchTab(tabIndex);
        }
    }

    private void SwitchTab(int index)
    {
        _activeTabIndex = index;
        _savedState.SelectedTab = index;
        StorageService.Save(_savedState);

        TabMediaView.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
        TabTrayView.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
        TabStatsView.Visibility = index == 2 ? Visibility.Visible : Visibility.Collapsed;
        TabTimerView.Visibility = index == 3 ? Visibility.Visible : Visibility.Collapsed;
        TabDockView.Visibility = index == 4 ? Visibility.Visible : Visibility.Collapsed;

        // Reactive avatar mood for tabs
        var tabMood = index switch
        {
            0 => AvatarState.Listening,
            1 => AvatarState.Curious,
            2 => AvatarState.Searching,
            3 => AvatarState.Working,
            4 => AvatarState.Proud,
            _ => AvatarState.Idle
        };
        TriggerAvatarReaction(tabMood, 4);

        UpdateTabButtonsStyle(index);
    }

    private void UpdateTabButtonsStyle(int index)
    {
        var lighter = Color.FromRgb(
            (byte)Math.Min(255, _currentAccentColor.R + 45),
            (byte)Math.Min(255, _currentAccentColor.G + 45),
            (byte)Math.Min(255, _currentAccentColor.B + 45));

        var activeTabBg = new LinearGradientBrush
        {
            StartPoint = new System.Windows.Point(0, 0),
            EndPoint = new System.Windows.Point(1, 1)
        };
        activeTabBg.GradientStops.Add(new GradientStop(Color.FromArgb(68, _currentAccentColor.R, _currentAccentColor.G, _currentAccentColor.B), 0.0));
        activeTabBg.GradientStops.Add(new GradientStop(Color.FromArgb(26, _currentAccentColor.R, _currentAccentColor.G, _currentAccentColor.B), 1.0));

        var activeTabBorder = new SolidColorBrush(Color.FromArgb(195, lighter.R, lighter.G, lighter.B));
        var inactiveTextBrush = new SolidColorBrush(Color.FromRgb(148, 163, 184));

        Button[] tabs = { TabBtnMedia, TabBtnTray, TabBtnStats, TabBtnTimer, TabBtnDock };
        for (int i = 0; i < tabs.Length; i++)
        {
            bool isActive = i == index;
            tabs[i].Background = isActive ? activeTabBg : Brushes.Transparent;
            tabs[i].BorderBrush = isActive ? activeTabBorder : Brushes.Transparent;
            tabs[i].Foreground = isActive ? Brushes.White : inactiveTextBrush;
        }

        // Update SVG tab icon strokes/fills
        TabIconMedia.Fill = index == 0 ? Brushes.White : inactiveTextBrush;
        TabIconTray.Stroke = index == 1 ? Brushes.White : inactiveTextBrush;
        TabIconStats.Stroke = index == 2 ? Brushes.White : inactiveTextBrush;
        TabIconTimer.Stroke = index == 3 ? Brushes.White : inactiveTextBrush;
        TabIconDock.Stroke = index == 4 ? Brushes.White : inactiveTextBrush;
    }
    #endregion

    #region Media & Audio Device Switcher Actions
    private bool _isCurrentTrackFavorited;

    private async void PlayPause_Click(object sender, RoutedEventArgs e) => await _mediaService.PlayPauseAsync();
    private async void NextTrack_Click(object sender, RoutedEventArgs e)
    {
        TriggerLiquidColorWave(_currentAccentColor);
        await _mediaService.NextAsync();
    }
    private async void PrevTrack_Click(object sender, RoutedEventArgs e)
    {
        TriggerLiquidColorWave(_currentAccentColor);
        await _mediaService.PreviousAsync();
    }

    private bool _isShuffleActive;
    private bool _isRepeatActive;

    private void ShuffleToggle_Click(object sender, RoutedEventArgs e)
    {
        _isShuffleActive = !_isShuffleActive;
        var activeBrush = new SolidColorBrush(Color.FromRgb(244, 63, 94));
        var inactiveBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225));
        if (ShuffleIconPath != null)
        {
            ShuffleIconPath.Stroke = _isShuffleActive ? activeBrush : inactiveBrush;
        }
        TriggerLiquidColorWave(_currentAccentColor);
    }

    private void RepeatToggle_Click(object sender, RoutedEventArgs e)
    {
        _isRepeatActive = !_isRepeatActive;
        var activeBrush = new SolidColorBrush(Color.FromRgb(244, 63, 94));
        var inactiveBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225));
        if (RepeatIconPath != null)
        {
            RepeatIconPath.Stroke = _isRepeatActive ? activeBrush : inactiveBrush;
        }
        TriggerLiquidColorWave(_currentAccentColor);
    }

    private void MiniHeartBtn_Click(object sender, RoutedEventArgs e)
    {
        FavoriteTrack_Click(sender, e);
    }

    private void FavoriteTrack_Click(object sender, RoutedEventArgs e)
    {
        _isCurrentTrackFavorited = !_isCurrentTrackFavorited;
        var favBrush = new SolidColorBrush(Color.FromRgb(244, 63, 94));
        var transBrush = Brushes.Transparent;
        var defaultStroke = new SolidColorBrush(Color.FromRgb(203, 213, 225));

        if (HeartIconPath != null)
        {
            HeartIconPath.Fill = _isCurrentTrackFavorited ? favBrush : transBrush;
            HeartIconPath.Stroke = _isCurrentTrackFavorited ? favBrush : defaultStroke;
        }
        if (MiniHeartPath != null)
        {
            MiniHeartPath.Fill = _isCurrentTrackFavorited ? favBrush : transBrush;
            MiniHeartPath.Stroke = _isCurrentTrackFavorited ? favBrush : defaultStroke;
        }
        if (_isCurrentTrackFavorited)
        {
            TriggerLiquidColorWave(Color.FromRgb(244, 63, 94));
        }
    }

    private async void SeekBackward_Click(object sender, RoutedEventArgs e)
    {
        double targetSec = Math.Max(0, TrackSeekSlider.Value - 10);
        TrackSeekSlider.Value = targetSec;
        CurrentTimeText.Text = FormatMediaTime(TimeSpan.FromSeconds(targetSec));
        await _mediaService.SeekAsync(TimeSpan.FromSeconds(targetSec));
    }

    private async void SeekForward_Click(object sender, RoutedEventArgs e)
    {
        double targetSec = Math.Min(TrackSeekSlider.Maximum, TrackSeekSlider.Value + 10);
        TrackSeekSlider.Value = targetSec;
        CurrentTimeText.Text = FormatMediaTime(TimeSpan.FromSeconds(targetSec));
        await _mediaService.SeekAsync(TimeSpan.FromSeconds(targetSec));
    }

    private void TrackSeekSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDraggingSeekSlider = true;
    }

    private async void TrackSeekSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDraggingSeekSlider)
        {
            _isDraggingSeekSlider = false;
            var target = TimeSpan.FromSeconds(TrackSeekSlider.Value);
            CurrentTimeText.Text = FormatMediaTime(target);
            await _mediaService.SeekAsync(target);
        }
    }

    private void TrackSeekSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isDraggingSeekSlider && IsLoaded)
        {
            CurrentTimeText.Text = FormatMediaTime(TimeSpan.FromSeconds(e.NewValue));
        }
    }

    private void MuteToggle_Click(object sender, RoutedEventArgs e)
    {
        bool next = AudioService.ToggleMute();
        _isMuted = next;
        MuteIconText.Text = next ? "🔇" : "🔊";
        SpeakerIconPath.Opacity = next ? 0.35 : 1.0;
        MiniVolText.Text = next ? "🔇 Sessiz" : $"🔊 {_masterVolume}%";
    }

    private void MasterVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (IsLoaded)
        {
            int vol = (int)Math.Round(e.NewValue);
            if (vol != _masterVolume)
            {
                AudioService.SetVolume(vol);
                _masterVolume = vol;
                if (MasterVolumePercentText != null) MasterVolumePercentText.Text = $"{vol}%";
                if (MiniVolText != null) MiniVolText.Text = $"🔊 {vol}%";
                if (MiniVolumeSlider != null && (int)Math.Round(MiniVolumeSlider.Value) != vol)
                {
                    MiniVolumeSlider.Value = vol;
                }
            }
        }
    }

    private void MiniVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (IsLoaded)
        {
            int vol = (int)Math.Round(e.NewValue);
            if (vol != _masterVolume)
            {
                AudioService.SetVolume(vol);
                _masterVolume = vol;
                if (MasterVolumePercentText != null) MasterVolumePercentText.Text = $"{vol}%";
                if (MiniVolText != null) MiniVolText.Text = $"🔊 {vol}%";
                if (MasterVolumeSlider != null && (int)Math.Round(MasterVolumeSlider.Value) != vol)
                {
                    MasterVolumeSlider.Value = vol;
                }
            }
        }
    }

    private async Task RefreshAudioDeviceLabelAsync()
    {
        var devices = await AudioService.GetOutputDevicesAsync();
        var def = devices.FirstOrDefault(d => d.IsDefault) ?? devices.FirstOrDefault();
        if (def != null)
        {
            AudioDeviceText.Text = $"🎧 {def.Name}";
        }
    }

    private async void SwitchAudioDevice_Click(object sender, RoutedEventArgs e)
    {
        string newDeviceName = await AudioService.CycleNextOutputDeviceAsync();
        AudioDeviceText.Text = $"🎧 {newDeviceName}";
        ShowToastBanner("🎧", "Ses Çıkışı Değişti", newDeviceName);
    }
    #endregion

    #region File Tray & Clipboard
    private void FileTray_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void FileTray_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop) && e.Data.GetData(DataFormats.FileDrop) is string[] files)
        {
            foreach (string file in files)
            {
                if (_savedState.TrayFiles.All(f => !string.Equals(f.FullPath, file, StringComparison.OrdinalIgnoreCase)))
                {
                    var item = StorageService.CreateTrayItem(file);
                    if (item != null)
                    {
                        _savedState.TrayFiles.Insert(0, item);
                    }
                }
            }
            StorageService.Save(_savedState);
            RefreshFileTrayUi();
        }
    }

    private void RefreshFileTrayUi()
    {
        TrayItemsPanel.Children.Clear();

        int count = _savedState.TrayFiles.Count;
        TrayCountText.Text = $"{count} Dosya";

        if (count == 0)
        {
            TrayEmptyText.Visibility = Visibility.Visible;
            TrayItemsPanel.Children.Add(TrayEmptyText);
            return;
        }

        TrayEmptyText.Visibility = Visibility.Collapsed;

        foreach (var file in _savedState.TrayFiles)
        {
            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(195, 15, 18, 30)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(32, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(9, 5, 9, 5),
                Margin = new Thickness(0, 2, 0, 3),
                Cursor = Cursors.Hand
            };

            card.MouseEnter += (s, e) =>
            {
                card.BorderBrush = new SolidColorBrush(Color.FromArgb(90, _currentAccentColor.R, _currentAccentColor.G, _currentAccentColor.B));
                card.Background = new SolidColorBrush(Color.FromArgb(220, 20, 24, 38));
            };
            card.MouseLeave += (s, e) =>
            {
                card.BorderBrush = new SolidColorBrush(Color.FromArgb(32, 255, 255, 255));
                card.Background = new SolidColorBrush(Color.FromArgb(195, 15, 18, 30));
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Dynamic badge color based on file type
            string ext = (file.ExtensionBadge ?? "").ToUpperInvariant();
            Color badgeBg;
            Color badgeBorder;
            if (ext is "PNG" or "JPG" or "JPEG" or "WEBP" or "GIF" or "BMP")
            {
                badgeBg = Color.FromArgb(45, 244, 63, 94); // rose
                badgeBorder = Color.FromArgb(90, 244, 63, 94);
            }
            else if (ext is "PDF" or "TXT" or "DOC" or "DOCX" or "MD")
            {
                badgeBg = Color.FromArgb(45, 56, 189, 248); // sky blue
                badgeBorder = Color.FromArgb(90, 56, 189, 248);
            }
            else if (ext is "ZIP" or "RAR" or "7Z" or "TAR")
            {
                badgeBg = Color.FromArgb(45, 245, 158, 11); // amber
                badgeBorder = Color.FromArgb(90, 245, 158, 11);
            }
            else if (ext is "EXE" or "MSI" or "BAT" or "CMD")
            {
                badgeBg = Color.FromArgb(45, 16, 185, 129); // emerald
                badgeBorder = Color.FromArgb(90, 16, 185, 129);
            }
            else
            {
                badgeBg = Color.FromArgb(45, _currentAccentColor.R, _currentAccentColor.G, _currentAccentColor.B);
                badgeBorder = Color.FromArgb(90, _currentAccentColor.R, _currentAccentColor.G, _currentAccentColor.B);
            }

            var badge = new Border
            {
                Background = new SolidColorBrush(badgeBg),
                BorderBrush = new SolidColorBrush(badgeBorder),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(5, 2, 5, 2),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = file.ExtensionBadge,
                    Foreground = Brushes.White,
                    FontSize = 9.5,
                    FontWeight = FontWeights.Bold
                }
            };
            Grid.SetColumn(badge, 0);
            grid.Children.Add(badge);

            var nameStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            nameStack.Children.Add(new TextBlock
            {
                Text = file.FileName,
                Foreground = Brushes.White,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 170
            });
            nameStack.Children.Add(new TextBlock
            {
                Text = file.SizeText,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                FontSize = 9
            });
            Grid.SetColumn(nameStack, 1);
            grid.Children.Add(nameStack);

            var btnStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            string targetPath = file.FullPath;
            var openBtn = new Button
            {
                Content = "Aç",
                Style = (Style)FindResource("IslandIconButton"),
                Padding = new Thickness(7, 3, 7, 3),
                FontSize = 10,
                Margin = new Thickness(0, 0, 4, 0),
                ToolTip = "Dosyayı Çalıştır / Aç"
            };
            openBtn.Click += (s, e) =>
            {
                try { Process.Start(new ProcessStartInfo(targetPath) { UseShellExecute = true }); } catch { }
            };
            btnStack.Children.Add(openBtn);

            var deleteBtn = new Button
            {
                Content = "✕",
                Style = (Style)FindResource("IslandIconButton"),
                Padding = new Thickness(6, 3, 6, 3),
                FontSize = 9.5,
                ToolTip = "Tepsiden Kaldır"
            };
            deleteBtn.Click += (s, e) =>
            {
                _savedState.TrayFiles.Remove(file);
                StorageService.Save(_savedState);
                RefreshFileTrayUi();
            };
            btnStack.Children.Add(deleteBtn);

            Grid.SetColumn(btnStack, 2);
            grid.Children.Add(btnStack);
            card.Child = grid;

            card.PreviewMouseMove += (s, e) =>
            {
                if (e.LeftButton == MouseButtonState.Pressed && File.Exists(targetPath))
                {
                    var data = new DataObject(DataFormats.FileDrop, new[] { targetPath });
                    DragDrop.DoDragDrop(card, data, DragDropEffects.Copy);
                }
            };

            TrayItemsPanel.Children.Add(card);
        }
    }

    private void CheckClipboardChanges()
    {
        try
        {
            if (Clipboard.ContainsText())
            {
                string text = Clipboard.GetText().Trim();
                if (!string.IsNullOrWhiteSpace(text) && text != _lastClipboardText)
                {
                    _lastClipboardText = text;
                    _savedState.ClipboardHistory.RemoveAll(t => t == text);
                    _savedState.ClipboardHistory.Insert(0, text);
                    if (_savedState.ClipboardHistory.Count > 10)
                    {
                        _savedState.ClipboardHistory = _savedState.ClipboardHistory.Take(10).ToList();
                    }
                    StorageService.Save(_savedState);
                    RefreshClipboardUi();
                }
            }
        }
        catch
        {
        }
    }

    private void RefreshClipboardUi()
    {
        ClipboardItemsPanel.Children.Clear();

        if (_savedState.ClipboardHistory.Count == 0)
        {
            ClipboardEmptyText.Visibility = Visibility.Visible;
            ClipboardItemsPanel.Children.Add(ClipboardEmptyText);
            return;
        }

        ClipboardEmptyText.Visibility = Visibility.Collapsed;

        foreach (string item in _savedState.ClipboardHistory)
        {
            var btn = new Button
            {
                Style = (Style)FindResource("IslandIconButton"),
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(9, 5, 9, 5),
                Margin = new Thickness(0, 2, 0, 3),
                Background = new SolidColorBrush(Color.FromArgb(195, 15, 18, 30)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Tag = item,
                ToolTip = "Panoya Kopyalamak İçin Tıklayın"
            };

            btn.MouseEnter += (s, e) =>
            {
                btn.BorderBrush = new SolidColorBrush(Color.FromArgb(90, 236, 72, 153));
                btn.Background = new SolidColorBrush(Color.FromArgb(220, 24, 20, 34));
            };
            btn.MouseLeave += (s, e) =>
            {
                btn.BorderBrush = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255));
                btn.Background = new SolidColorBrush(Color.FromArgb(195, 15, 18, 30));
            };

            var textBlock = new TextBlock
            {
                Text = item.Replace("\r", " ").Replace("\n", " "),
                Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                FontSize = 10.5,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 230
            };
            btn.Content = textBlock;

            string copyTarget = item;
            btn.Click += (s, e) =>
            {
                try
                {
                    Clipboard.SetText(copyTarget);
                    textBlock.Text = "✓ Panoya Kopyalandı!";
                    textBlock.Foreground = new SolidColorBrush(Color.FromRgb(52, 211, 153));
                }
                catch
                {
                }
            };

            ClipboardItemsPanel.Children.Add(btn);
        }
    }

    private void ClearClipboard_Click(object sender, RoutedEventArgs e)
    {
        _savedState.ClipboardHistory.Clear();
        StorageService.Save(_savedState);
        RefreshClipboardUi();
    }
    #endregion

    #region Quick Dock & Settings (Tab 4)
    private void RefreshDockUi()
    {
        DockAppsPanel.Children.Clear();

        foreach (var app in _savedState.QuickLaunchApps)
        {
            var btn = new Button
            {
                Style = (Style)FindResource("IslandIconButton"),
                Margin = new Thickness(3),
                Padding = new Thickness(10, 6, 10, 6),
                Background = new SolidColorBrush(Color.FromArgb(195, 17, 21, 35)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(35, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                ToolTip = $"{app.Name} ({app.Target}) — Kaldırmak için sağ tıklayın"
            };

            btn.MouseEnter += (s, e) =>
            {
                btn.BorderBrush = new SolidColorBrush(Color.FromArgb(110, _currentAccentColor.R, _currentAccentColor.G, _currentAccentColor.B));
                btn.Background = new SolidColorBrush(Color.FromArgb(230, 22, 27, 44));
            };
            btn.MouseLeave += (s, e) =>
            {
                btn.BorderBrush = new SolidColorBrush(Color.FromArgb(35, 255, 255, 255));
                btn.Background = new SolidColorBrush(Color.FromArgb(195, 17, 21, 35));
            };

            var stack = new StackPanel { Orientation = Orientation.Horizontal };
            stack.Children.Add(new TextBlock { Text = app.Icon, FontSize = 13, Margin = new Thickness(0, 0, 5, 0) });
            stack.Children.Add(new TextBlock { Text = app.Name, FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White });
            btn.Content = stack;

            string target = app.Target;
            btn.Click += (s, e) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
                    ShowToastBanner(app.Icon, app.Name, "Başlatılıyor...");
                }
                catch
                {
                }
            };

            btn.MouseRightButtonUp += (s, e) =>
            {
                _savedState.QuickLaunchApps.Remove(app);
                StorageService.Save(_savedState);
                RefreshDockUi();
            };

            DockAppsPanel.Children.Add(btn);
        }
    }

    private void Dock_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop) && e.Data.GetData(DataFormats.FileDrop) is string[] files)
        {
            foreach (string file in files)
            {
                string name = Path.GetFileNameWithoutExtension(file);
                if (string.IsNullOrWhiteSpace(name)) name = "Uygulama";
                _savedState.QuickLaunchApps.Add(new QuickLaunchItem
                {
                    Icon = "🚀",
                    Name = name.Length > 12 ? name[..12] : name,
                    Target = file
                });
            }
            StorageService.Save(_savedState);
            RefreshDockUi();
        }
    }

    private void MicMuteBtn_Click(object sender, RoutedEventArgs e)
    {
        bool nextMuted = AudioService.ToggleMicMute();
        UpdateMicUi(nextMuted);
        ShowToastBanner(
            nextMuted ? "🔇" : "🎙️",
            nextMuted ? "Mikrofon Susturuldu" : "Mikrofon Açıldı",
            nextMuted ? "Sesiniz karşıya gitmiyor" : "Mikrofon tekrar yayında");
    }

    private void BrightnessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded) return;
        int val = (int)Math.Round(e.NewValue);
        BrightnessPercentText.Text = $"{val}%";
        _savedState.ScreenBrightness = val;
        _screenOverlay.SetState(val, _savedState.WarmNightMode);
        StorageService.Save(_savedState);
    }

    private void NightModeBtn_Click(object sender, RoutedEventArgs e)
    {
        _savedState.WarmNightMode = !_savedState.WarmNightMode;
        _screenOverlay.SetState(_savedState.ScreenBrightness, _savedState.WarmNightMode);
        UpdateNightModeVisual();
        StorageService.Save(_savedState);
    }

    private void UpdateNightModeVisual()
    {
        NightModeBtnText.Text = _savedState.WarmNightMode ? "🌙 Gece: Açık" : "🌙 Gece";
        NightModeBtn.Background = _savedState.WarmNightMode
            ? new SolidColorBrush(Color.FromArgb(75, 245, 158, 11))
            : new SolidColorBrush(Color.FromArgb(18, 255, 255, 255));
    }

    private void GameModeToggle_Click(object sender, RoutedEventArgs e)
    {
        _savedState.AutoHideFullscreen = !_savedState.AutoHideFullscreen;
        if (!_savedState.AutoHideFullscreen && _isSleepingForFullscreen)
        {
            SetFullscreenSleepMode(false);
        }
        StorageService.Save(_savedState);
        UpdateSettingsVisuals();
    }

    private void SoundToggle_Click(object sender, RoutedEventArgs e)
    {
        // Cycle: Apple iOS -> Su Damlası -> Kristal Melodi -> Windows 11 -> Sessiz -> Apple iOS
        int next = ((int)SoundEffectsService.CurrentTheme + 1) % 5;
        var theme = (NotificationSoundTheme)next;
        SoundEffectsService.CurrentTheme = theme;
        _savedState.NotificationSoundTheme = next;
        _savedState.SoundEffectsEnabled = (theme != NotificationSoundTheme.Mute);
        SoundEffectsService.IsEnabled = _savedState.SoundEffectsEnabled;
        StorageService.Save(_savedState);
        UpdateSettingsVisuals();

        if (theme != NotificationSoundTheme.Mute)
        {
            SoundEffectsService.PlayPreview(theme);
            ShowToastBanner("🔔", "Ses Teması Değiştirildi", SoundEffectsService.GetThemeDisplayName(theme));
        }
        else
        {
            ShowToastBanner("🔕", "Sesler Kapatıldı", "Bildirim sesleri sessize alındı");
        }
    }

    private void StartupToggle_Click(object sender, RoutedEventArgs e)
    {
        StartupService.ToggleStartup();
        UpdateSettingsVisuals();
    }

    private void UpdateSettingsVisuals()
    {
        GameModeToggleText.Text = _savedState.AutoHideFullscreen
            ? "🎮 Oyun/Film Oto-Uyku: Açık"
            : "🎮 Oyun/Film Oto-Uyku: Kapalı";
        GameModeToggleText.Foreground = _savedState.AutoHideFullscreen
            ? new SolidColorBrush(Color.FromRgb(56, 189, 248))
            : new SolidColorBrush(Color.FromRgb(148, 163, 184));

        string themeName = SoundEffectsService.GetThemeDisplayName(SoundEffectsService.CurrentTheme);
        SoundToggleText.Text = SoundEffectsService.CurrentTheme != NotificationSoundTheme.Mute
            ? $"🔊 {themeName}"
            : "🔕 Ses: Kapalı (Sessiz)";
        SoundToggleText.Foreground = SoundEffectsService.CurrentTheme != NotificationSoundTheme.Mute
            ? new SolidColorBrush(Color.FromRgb(244, 114, 182))
            : new SolidColorBrush(Color.FromRgb(148, 163, 184));

        bool startup = StartupService.IsStartupEnabled();
        StartupToggleText.Text = startup
            ? "🚀 Başlangıçta Aç: Açık"
            : "🚀 Başlangıçta Aç: Kapalı";
    }
    #endregion

    #region Settings Panel Integration
    private Color? _themeOverrideColor;
    private SettingsWindow? _settingsWindow;

    public void OpenSettingsWindow()
    {
        Dispatcher.Invoke(() =>
        {
            if (_settingsWindow != null && _settingsWindow.IsLoaded)
            {
                _settingsWindow.Activate();
                if (_settingsWindow.WindowState == WindowState.Minimized)
                    _settingsWindow.WindowState = WindowState.Normal;
                return;
            }

            _settingsWindow = new SettingsWindow();
            _settingsWindow.Closed += (s, e) => _settingsWindow = null;
            _settingsWindow.Show();
            _settingsWindow.Activate();
        });
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        OpenSettingsWindow();
    }

    private void OpenSettingsPanel_Click(object sender, RoutedEventArgs e)
    {
        OpenSettingsWindow();
    }

    private void ApplyAllSettings(AppSettings s)
    {
        if (s == null) return;

        // 1. Appearance: Opacity & Glow
        IslandContainer.Opacity = Math.Clamp(s.Appearance.IslandOpacity / 100.0, 0.2, 1.0);

        double glowFactor = Math.Clamp(s.Appearance.IslandGlow / 100.0, 0.0, 1.0);
        AmbientGlowStop.Color = Color.FromArgb((byte)(56 * glowFactor), _currentAccentColor.R, _currentAccentColor.G, _currentAccentColor.B);
        AmbientGlowMidStop.Color = Color.FromArgb((byte)(20 * glowFactor), _currentAccentColor.R, _currentAccentColor.G, _currentAccentColor.B);
        AmbientGlowTopRightStop.Color = Color.FromArgb((byte)(36 * glowFactor), _currentAccentColor.R, _currentAccentColor.G, _currentAccentColor.B);
        IslandBorderAccentStop.Color = Color.FromArgb((byte)(176 * glowFactor), _currentAccentColor.R, _currentAccentColor.G, _currentAccentColor.B);

        // Theme override
        if (s.Appearance.Theme == "Yellow")
        {
            _themeOverrideColor = Color.FromRgb(245, 158, 11);
            ApplyAdaptiveAccentColor(_themeOverrideColor.Value);
        }
        else if (s.Appearance.Theme == "Custom")
        {
            try
            {
                _themeOverrideColor = (Color)ColorConverter.ConvertFromString(s.Appearance.CustomThemeColorHex);
                ApplyAdaptiveAccentColor(_themeOverrideColor.Value);
            }
            catch
            {
                _themeOverrideColor = null;
            }
        }
        else
        {
            _themeOverrideColor = null;
            if (_lastAppliedAmbientColor != Colors.Transparent)
            {
                ApplyAdaptiveAccentColor(_lastAppliedAmbientColor);
            }
        }

        // 2. Avatar
        MiniAvatarHostBorder.Visibility = s.Avatar.AvatarEnabled ? Visibility.Visible : Visibility.Collapsed;
        double avatarScale = Math.Clamp(s.Avatar.AvatarSize / 100.0, 0.7, 1.4);
        MiniAvatarViewbox.Width = 24 * avatarScale;
        MiniAvatarViewbox.Height = 24 * avatarScale;
        AvatarMiniViewbox.Width = 42 * avatarScale;
        AvatarMiniViewbox.Height = 42 * avatarScale;

        AvatarMiniHalo.Visibility = s.Avatar.AvatarGlow ? Visibility.Visible : Visibility.Collapsed;

        if (s.Avatar.AvatarAnimationSpeed == "Slow")
            _danceTimer.Interval = TimeSpan.FromMilliseconds(650);
        else if (s.Avatar.AvatarAnimationSpeed == "Fast")
            _danceTimer.Interval = TimeSpan.FromMilliseconds(280);
        else
            _danceTimer.Interval = TimeSpan.FromMilliseconds(460);

        if (!s.Avatar.IdleAnimation || !_isMediaActive || !_isMediaPlaying)
            _danceTimer.Stop();
        else if (!_danceTimer.IsEnabled)
            _danceTimer.Start();

        // 3. Mini Mode
        if (_isAvatarMiniMode)
        {
            double bSize = Math.Clamp(s.MiniMode.BubbleSize, 48, 76);
            IslandContainer.Width = bSize;
            IslandContainer.Height = bSize;
            IslandContainer.CornerRadius = new CornerRadius(bSize / 2.0);
            IslandClipGeometry.Rect = new Rect(0, 0, bSize, bSize);
            IslandClipGeometry.RadiusX = bSize / 2.0;
            IslandClipGeometry.RadiusY = bSize / 2.0;
            AvatarMiniModeView.Width = bSize;
            AvatarMiniModeView.Height = bSize;
            _lastAppliedAmbientColor = Colors.Transparent;
            CheckAndUpdateAmbientColor();
        }

        // 4. Behavior
        _savedState.AutoHideFullscreen = s.Behavior.FullscreenAutoHide;
        GameModeToggleText.Text = _savedState.AutoHideFullscreen ? "🎮 Oyun/Film Oto-Uyku: Açık" : "🎮 Oyun/Film Oto-Uyku: Kapalı";
        StartupToggleText.Text = s.Behavior.StartWithWindows ? "🚀 Başlangıçta Aç: Açık" : "🚀 Başlangıçta Aç: Kapalı";

        // 5. Performance
        if (s.Performance.PerformanceMode == "Low")
        {
            _loopTimer.Interval = TimeSpan.FromMilliseconds(1500);
            _ambientColorTimer.Interval = TimeSpan.FromMilliseconds(1800);
        }
        else if (s.Performance.PerformanceMode == "High")
        {
            _loopTimer.Interval = TimeSpan.FromMilliseconds(700);
            _ambientColorTimer.Interval = TimeSpan.FromMilliseconds(800);
        }
        else
        {
            _loopTimer.Interval = TimeSpan.FromMilliseconds(1000);
            _ambientColorTimer.Interval = TimeSpan.FromMilliseconds(1200);
        }
    }
    #endregion

    #region Timer & Quick Notes
    private void CountdownTimer_Tick(object? sender, EventArgs e)
    {
        if (_timerRemainingSeconds > 0)
        {
            _timerRemainingSeconds--;
            int m = _timerRemainingSeconds / 60;
            int s = _timerRemainingSeconds % 60;
            TimerDisplay.Text = $"{m:00}:{s:00}";
            UpdateTimerRingProgress((double)_timerRemainingSeconds / Math.Max(1, _timerTotalSeconds));
        }
        else
        {
            _isTimerRunning = false;
            _countdownTimer.Stop();
            if (TimerBtnLabel != null) TimerBtnLabel.Text = "▶ Başlat";
            else TimerStartPauseBtn.Content = "▶ Başlat";
            TimerDisplay.Text = "00:00";
            if (TimerStateText != null) TimerStateText.Text = "🎉 Tamamlandı!";
            UpdateTimerRingProgress(0.0);
            ShowToastBanner("🏆", "Pomodoro Tamamlandı!", "Tebrikler! +50 Odak XP kazandın.");
            TriggerAvatarReaction(AvatarState.Celebrate, 6);
        }
    }

    private void TimerStartPause_Click(object sender, RoutedEventArgs e)
    {
        if (_isTimerRunning)
        {
            _isTimerRunning = false;
            _countdownTimer.Stop();
            if (TimerBtnLabel != null) TimerBtnLabel.Text = "▶ Başlat";
            else TimerStartPauseBtn.Content = "▶ Başlat";
            if (TimerStateText != null) TimerStateText.Text = "⏸ Duraklatıldı";
            RestoreDefaultAvatarState();
        }
        else
        {
            if (_timerRemainingSeconds <= 0)
            {
                _timerRemainingSeconds = _timerTotalSeconds > 0 ? _timerTotalSeconds : 25 * 60;
            }
            _isTimerRunning = true;
            _countdownTimer.Start();
            if (TimerBtnLabel != null) TimerBtnLabel.Text = "⏸ Duraklat";
            else TimerStartPauseBtn.Content = "⏸ Duraklat";
            if (TimerStateText != null) TimerStateText.Text = "✦ Odaklanılıyor";
            TriggerAvatarReaction(AvatarState.Working, 0);

            UpdateTimerRingProgress((double)_timerRemainingSeconds / Math.Max(1, _timerTotalSeconds));
        }
    }

    private void TimerReset_Click(object sender, RoutedEventArgs e)
    {
        _isTimerRunning = false;
        _countdownTimer.Stop();
        if (TimerBtnLabel != null) TimerBtnLabel.Text = "▶ Başlat";
        else TimerStartPauseBtn.Content = "▶ Başlat";
        if (TimerStateText != null) TimerStateText.Text = "✦ Hazır";
        _timerRemainingSeconds = _timerTotalSeconds > 0 ? _timerTotalSeconds : 25 * 60;
        int m = _timerRemainingSeconds / 60;
        int s = _timerRemainingSeconds % 60;
        TimerDisplay.Text = $"{m:00}:{s:00}";
        UpdateTimerRingProgress(1.0);
    }

    private void TimerPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && int.TryParse(btn.Tag?.ToString(), out int mins))
        {
            _isTimerRunning = false;
            _countdownTimer.Stop();
            if (TimerBtnLabel != null) TimerBtnLabel.Text = "▶ Başlat";
            else TimerStartPauseBtn.Content = "▶ Başlat";
            if (TimerStateText != null) TimerStateText.Text = "✦ Hazır";
            _timerTotalSeconds = mins * 60;
            _timerRemainingSeconds = _timerTotalSeconds;
            TimerDisplay.Text = $"{mins:00}:00";
            UpdateTimerRingProgress(1.0);
        }
    }

    private void UpdateTimerRingProgress(double progress)
    {
        try
        {
            if (TimerRingArcPath == null) return;
            progress = Math.Clamp(progress, 0.0, 1.0);

            // Center: (48, 48), radius: 42 (fits inside 96x96 box)
            double cx = 48.0;
            double cy = 48.0;
            double r = 42.0;

            if (progress <= 0.0001)
            {
                TimerRingArcPath.Data = null;
                return;
            }

            double sweepAngle = progress * 359.99;
            double rad = (sweepAngle - 90.0) * Math.PI / 180.0;

            Point startPoint = new Point(cx, cy - r);
            Point endPoint = new Point(cx + r * Math.Cos(rad), cy + r * Math.Sin(rad));

            bool isLargeArc = sweepAngle > 180.0;

            var figure = new PathFigure
            {
                StartPoint = startPoint,
                IsClosed = false
            };
            figure.Segments.Add(new ArcSegment
            {
                Point = endPoint,
                Size = new Size(r, r),
                SweepDirection = SweepDirection.Clockwise,
                IsLargeArc = isLargeArc
            });

            var geom = new PathGeometry();
            geom.Figures.Add(figure);
            TimerRingArcPath.Data = geom;
        }
        catch
        {
        }
    }

    private void QuickNoteTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (IsLoaded)
        {
            _savedState.QuickNote = QuickNoteTextBox.Text;
            StorageService.Save(_savedState);
        }
    }
    #endregion

    #region Spotlight Quick Search & Calculator
    private List<SpotlightResultItem> _currentSpotlightResults = new();

    private void SpotlightToggle_Click(object sender, RoutedEventArgs e)
    {
        if (SpotlightOverlay.Visibility == Visibility.Visible)
        {
            CloseSpotlight();
        }
        else
        {
            OpenSpotlight();
        }
    }

    private void SpotlightClose_Click(object sender, RoutedEventArgs e)
    {
        CloseSpotlight();
    }

    private void OpenSpotlight()
    {
        if (!_isExpanded)
        {
            ExpandIsland(_activeTabIndex);
        }

        LyricsOverlay.Visibility = Visibility.Collapsed;
        SnapOverlay.Visibility = Visibility.Collapsed;
        SpotlightOverlay.Visibility = Visibility.Visible;
        SpotlightInputBox.Text = "";
        RenderSpotlightResults("");
        Activate();
        SpotlightInputBox.Focus();
    }

    private void CloseSpotlight()
    {
        SpotlightOverlay.Visibility = Visibility.Collapsed;
        SpotlightInputBox.Text = "";
    }

    private void SpotlightInputBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        RenderSpotlightResults(SpotlightInputBox.Text);
    }

    private void SpotlightInputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CloseSpotlight();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            if (_currentSpotlightResults.Count > 0)
            {
                ExecuteSpotlightResult(_currentSpotlightResults[0]);
                e.Handled = true;
            }
        }
    }

    private void RenderSpotlightResults(string? rawQuery)
    {
        string query = (rawQuery ?? "").Trim();
        SpotlightResultsPanel.Children.Clear();

        if (string.IsNullOrWhiteSpace(query))
        {
            _currentSpotlightResults.Clear();
            SpotlightResultsPanel.Children.Add(SpotlightDefaultHint);
            return;
        }

        _currentSpotlightResults = SpotlightService.Search(query);

        if (_currentSpotlightResults.Count == 0)
        {
            var emptyBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(14, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(28, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 2, 0, 2)
            };
            var emptyTb = new TextBlock
            {
                Text = $"'{query}' için sonuç bulunamadı.",
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            emptyBorder.Child = emptyTb;
            SpotlightResultsPanel.Children.Add(emptyBorder);
            return;
        }

        for (int i = 0; i < _currentSpotlightResults.Count; i++)
        {
            var item = _currentSpotlightResults[i];
            bool isTop = (i == 0);

            var card = new Border
            {
                Background = new SolidColorBrush(isTop ? Color.FromArgb(40, 255, 255, 255) : Color.FromArgb(14, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(isTop ? Color.FromArgb(80, 255, 255, 255) : Color.FromArgb(24, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10, 6, 10, 6),
                Margin = new Thickness(0, 2, 0, 2),
                Cursor = Cursors.Hand,
                Tag = item
            };

            card.MouseEnter += (s, ev) =>
            {
                if (s is Border b)
                {
                    b.Background = new SolidColorBrush(Color.FromArgb(55, 255, 255, 255));
                    b.BorderBrush = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255));
                }
            };
            card.MouseLeave += (s, ev) =>
            {
                if (s is Border b)
                {
                    bool top = ReferenceEquals(b.Tag, _currentSpotlightResults.FirstOrDefault());
                    b.Background = new SolidColorBrush(top ? Color.FromArgb(40, 255, 255, 255) : Color.FromArgb(14, 255, 255, 255));
                    b.BorderBrush = new SolidColorBrush(top ? Color.FromArgb(80, 255, 255, 255) : Color.FromArgb(24, 255, 255, 255));
                }
            };

            card.MouseLeftButtonUp += (s, ev) =>
            {
                if (s is Border b && b.Tag is SpotlightResultItem res)
                {
                    ExecuteSpotlightResult(res);
                }
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var iconTb = new TextBlock
            {
                Text = item.Icon,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            Grid.SetColumn(iconTb, 0);
            grid.Children.Add(iconTb);

            var textSp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var titleTb = new TextBlock
            {
                Text = item.Title,
                Foreground = Brushes.White,
                FontWeight = isTop ? FontWeights.SemiBold : FontWeights.Normal,
                FontSize = 11.5,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            var subTb = new TextBlock
            {
                Text = item.Subtitle,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                FontSize = 9.5,
                Margin = new Thickness(0, 1, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            textSp.Children.Add(titleTb);
            textSp.Children.Add(subTb);
            Grid.SetColumn(textSp, 1);
            grid.Children.Add(textSp);

            if (isTop)
            {
                var enterBadge = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                    CornerRadius = new CornerRadius(5),
                    Padding = new Thickness(6, 2, 6, 2),
                    VerticalAlignment = VerticalAlignment.Center
                };
                var enterTb = new TextBlock
                {
                    Text = "↵ Enter",
                    Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                    FontSize = 9,
                    FontWeight = FontWeights.Medium
                };
                enterBadge.Child = enterTb;
                Grid.SetColumn(enterBadge, 2);
                grid.Children.Add(enterBadge);
            }

            card.Child = grid;
            SpotlightResultsPanel.Children.Add(card);
        }
    }

    private void ExecuteSpotlightResult(SpotlightResultItem item)
    {
        try
        {
            switch (item.Type)
            {
                case SpotlightResultType.MathResult:
                    Clipboard.SetText(item.ActionPayload);
                    ShowToastBanner("🧮", "Panoya Kopyalandı", $"Sonuç: {item.ActionPayload}");
                    CloseSpotlight();
                    break;

                case SpotlightResultType.Application:
                    Process.Start(new ProcessStartInfo(item.ActionPayload) { UseShellExecute = true });
                    ShowToastBanner("🚀", item.Title, "Uygulama başlatılıyor...");
                    CloseSpotlight();
                    CollapseIsland();
                    break;

                case SpotlightResultType.WebGoogle:
                case SpotlightResultType.WebYouTube:
                    Process.Start(new ProcessStartInfo(item.ActionPayload) { UseShellExecute = true });
                    ShowToastBanner("🌐", "Web'de Açılıyor", item.Title);
                    CloseSpotlight();
                    CollapseIsland();
                    break;
            }
        }
        catch (Exception ex)
        {
            ShowToastBanner("⚠️", "İşlem Başarısız", ex.Message);
        }
    }
    #endregion

    #region Live Lyrics Handlers
    private async Task FetchLyricsAsync(string title, string artist, TimeSpan duration)
    {
        try
        {
            var data = await LyricsService.GetLyricsAsync(title, artist, duration);
            await Dispatcher.InvokeAsync(() =>
            {
                _currentLyrics = data;
                RenderLyricsOverlayContent();
            });
        }
        catch
        {
        }
    }

    private void RenderLyricsOverlayContent()
    {
        LyricsLinesPanel.Children.Clear();
        _renderedLyricTextBlocks.Clear();

        if (_currentLyrics == null || !_currentLyrics.Found)
        {
            LyricsLinesPanel.Children.Add(new TextBlock
            {
                Text = "Bu parça için şarkı sözü bulunamadı.",
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                FontSize = 11.5,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 24, 0, 0)
            });
            return;
        }

        if (_currentLyrics.HasSynced && _currentLyrics.Lines.Count > 0)
        {
            for (int i = 0; i < _currentLyrics.Lines.Count; i++)
            {
                var line = _currentLyrics.Lines[i];
                var tb = new TextBlock
                {
                    Text = line.Text,
                    Foreground = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)),
                    FontSize = 12,
                    FontWeight = FontWeights.Normal,
                    Margin = new Thickness(4, 3, 4, 3),
                    Cursor = Cursors.Hand,
                    Tag = line.Time
                };

                tb.MouseLeftButtonUp += (s, e) =>
                {
                    if (s is TextBlock t && t.Tag is TimeSpan time)
                    {
                        _ = _mediaService.SeekAsync(time);
                    }
                };

                _renderedLyricTextBlocks.Add(tb);
                LyricsLinesPanel.Children.Add(tb);
            }
        }
        else if (!string.IsNullOrWhiteSpace(_currentLyrics.PlainLyrics))
        {
            var plainTb = new TextBlock
            {
                Text = _currentLyrics.PlainLyrics,
                Foreground = Brushes.White,
                FontSize = 11.5,
                LineHeight = 18,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(6, 4, 6, 4)
            };
            LyricsLinesPanel.Children.Add(plainTb);
        }
    }

    private void UpdateLiveLyricsDisplay(TimeSpan position)
    {
        if (_currentLyrics == null) return;

        if (_currentLyrics.HasSynced && _currentLyrics.Lines.Count > 0)
        {
            int idx = LyricsService.GetCurrentLineIndex(_currentLyrics, position);
            if (idx >= 0 && idx < _currentLyrics.Lines.Count)
            {
                LiveCurrentLyricText.Text = _currentLyrics.Lines[idx].Text;

                if (LyricsOverlay.Visibility == Visibility.Visible && idx != _lastActiveLyricIndex)
                {
                    _lastActiveLyricIndex = idx;

                    for (int i = 0; i < _renderedLyricTextBlocks.Count; i++)
                    {
                        var tb = _renderedLyricTextBlocks[i];
                        if (i == idx)
                        {
                            tb.Foreground = Brushes.White;
                            tb.FontWeight = FontWeights.Bold;
                            tb.FontSize = 13.5;
                            tb.BringIntoView();
                        }
                        else if (Math.Abs(i - idx) <= 1)
                        {
                            tb.Foreground = new SolidColorBrush(Color.FromArgb(170, 255, 255, 255));
                            tb.FontWeight = FontWeights.Medium;
                            tb.FontSize = 12;
                        }
                        else
                        {
                            tb.Foreground = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255));
                            tb.FontWeight = FontWeights.Normal;
                            tb.FontSize = 11.5;
                        }
                    }
                }
            }
            else if (idx < 0)
            {
                // In intro / before first lyric line begins
                if (_currentLyrics.Lines.Count > 0)
                {
                    LiveCurrentLyricText.Text = $"🎵 {_currentLyrics.Lines[0].Text}";
                }
                else
                {
                    LiveCurrentLyricText.Text = "🎵 Giriş...";
                }
            }
        }
        else if (_currentLyrics.Found && !string.IsNullOrWhiteSpace(_currentLyrics.PlainLyrics))
        {
            var firstLine = _currentLyrics.PlainLyrics.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            LiveCurrentLyricText.Text = string.IsNullOrEmpty(firstLine) ? "🎵 Şarkı sözleri mevcut" : $"🎵 {firstLine}";
        }
        else
        {
            LiveCurrentLyricText.Text = "Şarkı sözü bulunamadı";
        }
    }

    private void LyricsToggle_Click(object sender, RoutedEventArgs e)
    {
        if (LyricsOverlay.Visibility == Visibility.Visible)
        {
            CloseLyrics();
        }
        else
        {
            OpenLyrics();
        }
    }

    private void LyricsPill_Click(object sender, MouseButtonEventArgs e)
    {
        OpenLyrics();
    }

    private void LyricsClose_Click(object sender, RoutedEventArgs e)
    {
        CloseLyrics();
    }

    private void OpenLyrics()
    {
        if (!_isExpanded)
        {
            ExpandIsland(0);
        }

        SpotlightOverlay.Visibility = Visibility.Collapsed;
        SnapOverlay.Visibility = Visibility.Collapsed;
        LyricsOverlay.Visibility = Visibility.Visible;

        RenderLyricsOverlayContent();
    }

    private void CloseLyrics()
    {
        LyricsOverlay.Visibility = Visibility.Collapsed;
    }
    #endregion

    #region Window Snap Service Handlers
    private void SnapToggle_Click(object sender, RoutedEventArgs e)
    {
        if (SnapOverlay.Visibility == Visibility.Visible)
        {
            CloseSnap();
        }
        else
        {
            OpenSnap();
        }
    }

    private void SnapClose_Click(object sender, RoutedEventArgs e)
    {
        CloseSnap();
    }

    private void OpenSnap()
    {
        if (!_isExpanded)
        {
            ExpandIsland(_activeTabIndex);
        }

        SpotlightOverlay.Visibility = Visibility.Collapsed;
        LyricsOverlay.Visibility = Visibility.Collapsed;
        SnapOverlay.Visibility = Visibility.Visible;

        string title = WindowSnapService.GetWindowTitle(_lastTargetWindow);
        SnapTargetWindowTitle.Text = string.IsNullOrWhiteSpace(title) ? "Aktif Pencere" : title;
    }

    private void CloseSnap()
    {
        SnapOverlay.Visibility = Visibility.Collapsed;
    }

    private void SnapLeftHalf_Click(object sender, RoutedEventArgs e)
    {
        bool ok = WindowSnapService.SnapWindow(_lastTargetWindow, WindowSnapMode.LeftHalf);
        if (ok)
        {
            ShowToastBanner("🪟", "Pencere Hizalandı", "Sol %50");
            SoundEffectsService.PlayChime();
        }
        CloseSnap();
    }

    private void SnapRightHalf_Click(object sender, RoutedEventArgs e)
    {
        bool ok = WindowSnapService.SnapWindow(_lastTargetWindow, WindowSnapMode.RightHalf);
        if (ok)
        {
            ShowToastBanner("🪟", "Pencere Hizalandı", "Sağ %50");
            SoundEffectsService.PlayChime();
        }
        CloseSnap();
    }

    private void SnapLeftThird_Click(object sender, RoutedEventArgs e)
    {
        bool ok = WindowSnapService.SnapWindow(_lastTargetWindow, WindowSnapMode.LeftThird);
        if (ok)
        {
            ShowToastBanner("🪟", "Pencere Hizalandı", "Sol 1/3 (Panel)");
            SoundEffectsService.PlayChime();
        }
        CloseSnap();
    }

    private void SnapRightTwoThirds_Click(object sender, RoutedEventArgs e)
    {
        bool ok = WindowSnapService.SnapWindow(_lastTargetWindow, WindowSnapMode.RightTwoThirds);
        if (ok)
        {
            ShowToastBanner("🪟", "Pencere Hizalandı", "Sağ 2/3 (Geniş)");
            SoundEffectsService.PlayChime();
        }
        CloseSnap();
    }

    private void SnapCenterFloat_Click(object sender, RoutedEventArgs e)
    {
        bool ok = WindowSnapService.SnapWindow(_lastTargetWindow, WindowSnapMode.CenterFloat);
        if (ok)
        {
            ShowToastBanner("🪟", "Pencere Hizalandı", "Merkez Odak (%72)");
            SoundEffectsService.PlayChime();
        }
        CloseSnap();
    }

    private void SnapMaximize_Click(object sender, RoutedEventArgs e)
    {
        bool ok = WindowSnapService.SnapWindow(_lastTargetWindow, WindowSnapMode.Maximize);
        if (ok)
        {
            ShowToastBanner("🪟", "Pencere Hizalandı", "Tam Ekran (%100)");
            SoundEffectsService.PlayChime();
        }
        CloseSnap();
    }
    #endregion

    #region Avatar Integration & Native Vector Controller System
    private void InitializeNativeAvatar()
    {
        try
        {
            _avatarReactionTimer.Interval = TimeSpan.FromSeconds(8);
            _avatarReactionTimer.Tick += (s, e) =>
            {
                _avatarReactionTimer.Stop();
                RestoreDefaultAvatarState();
            };

            _nativeAvatar.FrameUpdated += (head, leftEye, rightEye, leftVis, rightVis) =>
            {
                // 1. Collapsed island normal avatar
                MiniAvatarHeadPath.Data = head;
                MiniAvatarLeftEyePath.Data = leftEye;
                MiniAvatarRightEyePath.Data = rightEye;
                MiniAvatarLeftEyePath.Visibility = leftVis ? Visibility.Visible : Visibility.Collapsed;
                MiniAvatarRightEyePath.Visibility = rightVis ? Visibility.Visible : Visibility.Collapsed;

                // 2. Dedicated Avatar Mini Bubble (Mini Mode)
                MiniBubbleHeadPath.Data = head;
                MiniBubbleLeftEyePath.Data = leftEye;
                MiniBubbleRightEyePath.Data = rightEye;
                MiniBubbleLeftEyePath.Visibility = leftVis ? Visibility.Visible : Visibility.Collapsed;
                MiniBubbleRightEyePath.Visibility = rightVis ? Visibility.Visible : Visibility.Collapsed;

                // 3. Expanded Stage Onee Companion (Right side of media player)
                if (ExpandedStageHeadPath != null) ExpandedStageHeadPath.Data = head;
                if (ExpandedStageLeftEyePath != null)
                {
                    ExpandedStageLeftEyePath.Data = leftEye;
                    ExpandedStageLeftEyePath.Visibility = leftVis ? Visibility.Visible : Visibility.Collapsed;
                }
                if (ExpandedStageRightEyePath != null)
                {
                    ExpandedStageRightEyePath.Data = rightEye;
                    ExpandedStageRightEyePath.Visibility = rightVis ? Visibility.Visible : Visibility.Collapsed;
                }

                // 4. Expanded overlay
                if (AvatarOverlay.Visibility == Visibility.Visible)
                {
                    LargeAvatarHeadPath.Data = head;
                    LargeAvatarLeftEyePath.Data = leftEye;
                    LargeAvatarRightEyePath.Data = rightEye;
                    LargeAvatarLeftEyePath.Visibility = leftVis ? Visibility.Visible : Visibility.Collapsed;
                    LargeAvatarRightEyePath.Visibility = rightVis ? Visibility.Visible : Visibility.Collapsed;
                }
            };

            UpdateAvatarBrushes(_currentAccentColor);
            _nativeAvatar.Start("idle");
            AvatarCurrentAnimLabel.Text = " • idle";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MainWindow] InitializeNativeAvatar error: {ex.Message}");
        }
    }

    private void RestoreDefaultAvatarState()
    {
        if (_isTimerRunning)
        {
            _nativeAvatar.Play("working");
            AvatarCurrentAnimLabel.Text = " • working";
            return;
        }

        if (_isMediaActive && _isMediaPlaying)
        {
            _nativeAvatar.Play("listening");
            AvatarCurrentAnimLabel.Text = " • listening";
            return;
        }

        // 1. High CPU / GPU Load (>= 85%) -> Drowsy / Bored (tired from heavy work)
        if (_lastSystemStats != null && (_lastSystemStats.CpuPercent >= 85 || _lastSystemStats.GpuPercent >= 85))
        {
            _nativeAvatar.Play("drowsy");
            AvatarCurrentAnimLabel.Text = " • yorgun (yüksek yük)";
            return;
        }

        // 2. Critical Low Battery (<= 15% and not charging) -> Sad (hungry for power)
        if (_lastSystemStats != null && _lastSystemStats.HasBattery && _lastSystemStats.BatteryPercent <= 15 && !_lastSystemStats.IsCharging)
        {
            _nativeAvatar.Play("sad");
            AvatarCurrentAnimLabel.Text = " • acıktı (düşük pil)";
            return;
        }

        // 3. Late Night (23:00 - 06:00) -> Sleeping & Atmospheric Night Mode
        int hour = DateTime.Now.Hour;
        if (hour >= 23 || hour < 6)
        {
            _nativeAvatar.Play("sleeping");
            AvatarCurrentAnimLabel.Text = " • uyuyor (gece modu)";
            if (OneeNightSleepIndicator != null) { OneeNightSleepIndicator.Visibility = Visibility.Visible; OneeNightSleepIndicator.Opacity = 1.0; }
            if (MiniBubbleNightSleepIndicator != null) { MiniBubbleNightSleepIndicator.Visibility = Visibility.Visible; MiniBubbleNightSleepIndicator.Opacity = 1.0; }
            if (OneeThoughtBadge != null) { OneeThoughtBadge.Visibility = Visibility.Visible; OneeThoughtBadge.Opacity = 0.9; }
            if (MiniBubbleThoughtBadge != null) { MiniBubbleThoughtBadge.Visibility = Visibility.Visible; MiniBubbleThoughtBadge.Opacity = 0.9; }
            return;
        }
        else
        {
            if (OneeNightSleepIndicator != null) OneeNightSleepIndicator.Visibility = Visibility.Collapsed;
            if (MiniBubbleNightSleepIndicator != null) MiniBubbleNightSleepIndicator.Visibility = Visibility.Collapsed;
            if (OneeThoughtBadge != null) OneeThoughtBadge.Visibility = Visibility.Collapsed;
            if (MiniBubbleThoughtBadge != null) MiniBubbleThoughtBadge.Visibility = Visibility.Collapsed;
        }

        _nativeAvatar.Play("idle");
        AvatarCurrentAnimLabel.Text = " • idle";
    }

    public void TriggerAvatarReaction(AvatarState state, int durationSeconds = 6)
    {
        if (!SettingsService.Current.Avatar.ReactionAnimations) return;

        string anim = state switch
        {
            AvatarState.Idle => "idle",
            AvatarState.Listening => "listening",
            AvatarState.Thinking => "thinking",
            AvatarState.Searching => "searching",
            AvatarState.Working => "working",
            AvatarState.Happy => "happy",
            AvatarState.Excited => "excited",
            AvatarState.Surprised => "surprised",
            AvatarState.Confused => "confused",
            AvatarState.Bored => "bored",
            AvatarState.Angry => "angry",
            AvatarState.Drowsy => "drowsy",
            AvatarState.Curious => "curious",
            AvatarState.Proud => "proud",
            AvatarState.Shy => "shy",
            AvatarState.Sad => "sad",
            AvatarState.Laughing => "laughing",
            AvatarState.Scared => "scared",
            AvatarState.Playful => "playful",
            AvatarState.Celebrate => "celebrate",
            AvatarState.Sleeping => "sleeping",
            AvatarState.Waking => "waking",
            _ => "idle"
        };

        _nativeAvatar.Play(anim);
        AvatarCurrentAnimLabel.Text = $" • {anim}";
        UpdateMiniMoodBadge(state);

        _avatarReactionTimer.Stop();
        if (durationSeconds > 0)
        {
            _avatarReactionTimer.Interval = TimeSpan.FromSeconds(durationSeconds);
            _avatarReactionTimer.Start();
        }
    }

    private void MiniAvatar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (_isAvatarMiniMode)
        {
            ExitAvatarMiniMode();
            return;
        }
        if (_isExpanded)
        {
            AvatarToggle_Click(sender, e);
        }
        else
        {
            TriggerPokePetEffect();
            CycleAvatarMood();
        }
    }

    private int _avatarMoodIndex = 0;
    private static readonly AvatarState[] QuickMoods = new[]
    {
        AvatarState.Happy,
        AvatarState.Curious,
        AvatarState.Excited,
        AvatarState.Surprised,
        AvatarState.Playful,
        AvatarState.Proud,
        AvatarState.Idle
    };

    private void CycleAvatarMood()
    {
        _avatarMoodIndex = (_avatarMoodIndex + 1) % QuickMoods.Length;
        var mood = QuickMoods[_avatarMoodIndex];
        TriggerAvatarReaction(mood, 5);
        SoundEffectsService.PlayTick();
    }

    private void AvatarToggle_Click(object sender, RoutedEventArgs e)
    {
        if (AvatarOverlay.Visibility == Visibility.Visible)
        {
            CloseAvatarOverlay();
        }
        else
        {
            OpenAvatarOverlay();
        }
    }

    private void AvatarClose_Click(object sender, RoutedEventArgs e)
    {
        CloseAvatarOverlay();
    }

    private void OpenAvatarOverlay()
    {
        if (!_isExpanded)
        {
            ExpandIsland(_activeTabIndex);
        }

        SpotlightOverlay.Visibility = Visibility.Collapsed;
        LyricsOverlay.Visibility = Visibility.Collapsed;
        SnapOverlay.Visibility = Visibility.Collapsed;
        AvatarOverlay.Visibility = Visibility.Visible;

        AvatarCurrentAnimLabel.Text = $" • {_nativeAvatar.CurrentAnimation}";
    }

    private void CloseAvatarOverlay()
    {
        AvatarOverlay.Visibility = Visibility.Collapsed;
    }

    private void AvatarEmotionButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string animName)
        {
            _nativeAvatar.Play(animName);
            AvatarCurrentAnimLabel.Text = $" • {animName}";
            SoundEffectsService.PlayTick();

            _avatarReactionTimer.Stop();
            _avatarReactionTimer.Interval = TimeSpan.FromSeconds(10);
            _avatarReactionTimer.Start();
        }
    }
    #endregion

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        try
        {
            var helper = new WindowInteropHelper(this);
            if (helper.Handle != IntPtr.Zero)
            {
                UnregisterHotKey(helper.Handle, HOTKEY_ID_ALT_SPACE);
                UnregisterHotKey(helper.Handle, HOTKEY_ID_CTRL_SPACE);
                UnregisterHotKey(helper.Handle, HOTKEY_ID_CTRL_TILDE);
            }
            _hwndSource?.RemoveHook(HwndHook);
            _screenOverlay.CloseOverlay();
            _notifyIcon?.Dispose();
            _loopTimer.Stop();
            _countdownTimer.Stop();
            _snipWatchTimer.Stop();
            _danceTimer.Stop();
            _speechBubbleTimer.Stop();
            _miniHoldTimer.Stop();
        }
        catch
        {
        }
    }

    private void PillHome_Click(object sender, RoutedEventArgs e)
    {
        RestoreDefaultAvatarState();
        _nativeAvatar.Play("happy");
        SoundEffectsService.PlayPoke();
        TriggerPokePetEffect();
        ShowToastBanner("🐾", "Mochi Uyanık!", "Mochi sana sevgiyle el sallıyor!");
    }

    private void PillChat_Click(object sender, RoutedEventArgs e)
    {
        TriggerAvatarReaction(AvatarState.Curious, 4);
        if (OneeThoughtBadge != null)
        {
            OneeThoughtBadge.Visibility = Visibility.Visible;
            OneeThoughtBadge.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)));
        }
        ShowToastBanner("💬", "Mochi Düşünüyor...", "Harika bir gün geçiriyoruz! Seninle çalışmak çok güzel.");
    }

    private void PillDesktop_Click(object sender, RoutedEventArgs e)
    {
        EnterAvatarMiniMode();
    }

    private void PillSleep_Click(object sender, RoutedEventArgs e)
    {
        if (_isStealthRetracted)
        {
            ExitStealthNotch();
        }
        else
        {
            EnterStealthNotch();
        }
    }

    private void PillExpand_Click(object sender, RoutedEventArgs e)
    {
        ExpandIsland();
    }



    private void MiniMode_Click(object sender, RoutedEventArgs e)
    {
        EnterAvatarMiniMode();
    }

}

public static class VisualTreeExtensions
{
    public static T? FindVisualParent<T>(this DependencyObject? child) where T : DependencyObject
    {
        while (child != null)
        {
            if (child is T parent) return parent;
            child = VisualTreeHelper.GetParent(child);
        }
        return null;
    }
}

public class CornerRadiusAnimation : AnimationTimeline
{
    public static readonly DependencyProperty FromProperty =
        DependencyProperty.Register(nameof(From), typeof(CornerRadius?), typeof(CornerRadiusAnimation));

    public static readonly DependencyProperty ToProperty =
        DependencyProperty.Register(nameof(To), typeof(CornerRadius), typeof(CornerRadiusAnimation));

    public static readonly DependencyProperty EasingFunctionProperty =
        DependencyProperty.Register(nameof(EasingFunction), typeof(IEasingFunction), typeof(CornerRadiusAnimation));

    public override Type TargetPropertyType => typeof(CornerRadius);

    protected override Freezable CreateInstanceCore() => new CornerRadiusAnimation();

    public CornerRadius? From
    {
        get => (CornerRadius?)GetValue(FromProperty);
        set => SetValue(FromProperty, value);
    }

    public CornerRadius To
    {
        get => (CornerRadius)GetValue(ToProperty);
        set => SetValue(ToProperty, value);
    }

    public IEasingFunction? EasingFunction
    {
        get => (IEasingFunction?)GetValue(EasingFunctionProperty);
        set => SetValue(EasingFunctionProperty, value);
    }

    public CornerRadiusAnimation() { }

    public CornerRadiusAnimation(CornerRadius to, Duration duration)
    {
        To = to;
        Duration = duration;
    }

    public CornerRadiusAnimation(CornerRadius from, CornerRadius to, Duration duration)
    {
        From = from;
        To = to;
        Duration = duration;
    }

    public override object GetCurrentValue(object defaultOriginValue, object defaultDestinationValue, AnimationClock animationClock)
    {
        double progress = animationClock.CurrentProgress ?? 0;
        var easing = EasingFunction;
        if (easing != null)
        {
            progress = easing.Ease(progress);
        }

        var from = From ?? (CornerRadius)defaultOriginValue;
        var to = To;

        double tl = from.TopLeft + (to.TopLeft - from.TopLeft) * progress;
        double tr = from.TopRight + (to.TopRight - from.TopRight) * progress;
        double br = from.BottomRight + (to.BottomRight - from.BottomRight) * progress;
        double bl = from.BottomLeft + (to.BottomLeft - from.BottomLeft) * progress;

        return new CornerRadius(Math.Max(0, tl), Math.Max(0, tr), Math.Max(0, br), Math.Max(0, bl));
    }
}
