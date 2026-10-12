using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DynamicIslandWin.Services;

namespace DynamicIslandWin;

public partial class SettingsWindow : Window
{
    private bool _isInitializing = true;

    public SettingsWindow()
    {
        InitializeComponent();
        LoadSettingsIntoUi();
        _isInitializing = false;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    #region Navigation Tabs
    private void NavTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string section)
        {
            SwitchSection(section);
        }
    }

    private void SwitchSection(string section)
    {
        SectionAppearance.Visibility = section == "Appearance" ? Visibility.Visible : Visibility.Collapsed;
        SectionAvatar.Visibility = section == "Avatar" ? Visibility.Visible : Visibility.Collapsed;
        SectionMiniMode.Visibility = section == "MiniMode" ? Visibility.Visible : Visibility.Collapsed;
        SectionBehavior.Visibility = section == "Behavior" ? Visibility.Visible : Visibility.Collapsed;
        SectionPerformance.Visibility = section == "Performance" ? Visibility.Visible : Visibility.Collapsed;

        HighlightTabButton(TabBtnAppearance, section == "Appearance");
        HighlightTabButton(TabBtnAvatar, section == "Avatar");
        HighlightTabButton(TabBtnMini, section == "MiniMode");
        HighlightTabButton(TabBtnBehavior, section == "Behavior");
        HighlightTabButton(TabBtnPerf, section == "Performance");
    }

    private void HighlightTabButton(Button btn, bool isActive)
    {
        if (isActive)
        {
            btn.Background = new SolidColorBrush(Color.FromArgb(45, 244, 63, 94));
            btn.Foreground = Brushes.White;
            btn.BorderBrush = new SolidColorBrush(Color.FromArgb(180, 244, 63, 94));
        }
        else
        {
            btn.Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255));
            btn.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
            btn.BorderBrush = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255));
        }
    }
    #endregion

    #region Load Settings into UI
    private void LoadSettingsIntoUi()
    {
        var s = SettingsService.Current;

        // 1. Appearance
        SliderIslandGlow.Value = s.Appearance.IslandGlow;
        TxtGlowVal.Text = $"%{(int)s.Appearance.IslandGlow}";

        SliderIslandOpacity.Value = s.Appearance.IslandOpacity;
        TxtOpacityVal.Text = $"%{(int)s.Appearance.IslandOpacity}";

        SelectSegment(new[] { BtnAnimSpeedSlow, BtnAnimSpeedNormal, BtnAnimSpeedFast }, s.Appearance.AnimationSpeed);

        ChkAdaptiveGlow.IsChecked = s.Appearance.AdaptiveGlow;
        SelectSegment(new[] { BtnThemeAdaptive, BtnThemeYellow, BtnThemeCustom }, s.Appearance.Theme);
        CustomColorPanel.Visibility = s.Appearance.Theme == "Custom" ? Visibility.Visible : Visibility.Collapsed;
        TxtCustomHex.Text = s.Appearance.CustomThemeColorHex;

        // 2. Avatar
        ChkAvatarEnabled.IsChecked = s.Avatar.AvatarEnabled;
        SliderAvatarSize.Value = s.Avatar.AvatarSize;
        TxtAvatarSizeVal.Text = $"%{(int)s.Avatar.AvatarSize}";
        ChkAvatarIdle.IsChecked = s.Avatar.IdleAnimation;
        ChkAvatarReactions.IsChecked = s.Avatar.ReactionAnimations;
        ChkAvatarGlow.IsChecked = s.Avatar.AvatarGlow;
        SelectSegment(new[] { BtnAvatarSpeedSlow, BtnAvatarSpeedNormal, BtnAvatarSpeedFast }, s.Avatar.AvatarAnimationSpeed);

        // 3. Mini Mode
        ChkMiniModeEnabled.IsChecked = s.MiniMode.MiniModeEnabled;
        ChkRightClickMini.IsChecked = s.MiniMode.RightClickMiniMode;
        SliderBubbleSize.Value = s.MiniMode.BubbleSize;
        TxtBubbleSizeVal.Text = $"{(int)s.MiniMode.BubbleSize} px";
        SelectSegment(new[] { BtnPosTopRight, BtnPosTopLeft }, s.MiniMode.BubblePosition);
        SelectSegment(new[] { BtnMiniSpeedSlow, BtnMiniSpeedNormal, BtnMiniSpeedFast }, s.MiniMode.TransitionSpeed);
        ChkFluidMorph.IsChecked = s.MiniMode.FluidMorph;

        // 4. Behavior
        ChkStartWithWindows.IsChecked = s.Behavior.StartWithWindows;
        ChkShowNotifications.IsChecked = s.Behavior.ShowNotifications;
        ChkMusicReactions.IsChecked = s.Behavior.MusicReactions;
        ChkBatteryReactions.IsChecked = s.Behavior.BatteryReactions;
        ChkVolumeReactions.IsChecked = s.Behavior.VolumeReactions;
        ChkFullscreenAutoHide.IsChecked = s.Behavior.FullscreenAutoHide;

        // 5. Performance
        SelectSegment(new[] { BtnPerfLow, BtnPerfBalanced, BtnPerfHigh }, s.Performance.PerformanceMode);
        UpdatePerfDescription(s.Performance.PerformanceMode);
        ChkReduceMotion.IsChecked = s.Performance.ReduceMotion;

        // Default tab
        SwitchSection("Appearance");
    }

    private void SelectSegment(Button[] buttons, string selectedTag)
    {
        foreach (var b in buttons)
        {
            bool isSelected = string.Equals(b.Tag?.ToString(), selectedTag, StringComparison.OrdinalIgnoreCase);
            if (isSelected)
            {
                b.Background = new SolidColorBrush(Color.FromArgb(50, 244, 63, 94));
                b.Foreground = Brushes.White;
                b.BorderBrush = new SolidColorBrush(Color.FromArgb(200, 244, 63, 94));
            }
            else
            {
                b.Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255));
                b.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
                b.BorderBrush = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255));
            }
        }
    }
    #endregion

    #region Appearance Event Handlers
    private void SliderIslandGlow_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isInitializing) return;
        TxtGlowVal.Text = $"%{(int)e.NewValue}";
        SettingsService.Current.Appearance.IslandGlow = e.NewValue;
        SettingsService.NotifyChanged();
    }

    private void SliderIslandOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isInitializing) return;
        TxtOpacityVal.Text = $"%{(int)e.NewValue}";
        SettingsService.Current.Appearance.IslandOpacity = e.NewValue;
        SettingsService.NotifyChanged();
    }

    private void AnimSpeed_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string speed)
        {
            SelectSegment(new[] { BtnAnimSpeedSlow, BtnAnimSpeedNormal, BtnAnimSpeedFast }, speed);
            SettingsService.Current.Appearance.AnimationSpeed = speed;
            SettingsService.NotifyChanged();
        }
    }

    private void ChkAdaptiveGlow_Click(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        SettingsService.Current.Appearance.AdaptiveGlow = ChkAdaptiveGlow.IsChecked ?? true;
        SettingsService.NotifyChanged();
    }

    private void ThemeBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string theme)
        {
            SelectSegment(new[] { BtnThemeAdaptive, BtnThemeYellow, BtnThemeCustom }, theme);
            SettingsService.Current.Appearance.Theme = theme;
            CustomColorPanel.Visibility = theme == "Custom" ? Visibility.Visible : Visibility.Collapsed;
            SettingsService.NotifyChanged();
        }
    }

    private void PresetColor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string hex)
        {
            TxtCustomHex.Text = hex;
            SettingsService.Current.Appearance.CustomThemeColorHex = hex;
            SettingsService.NotifyChanged();
        }
    }

    private void TxtCustomHex_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isInitializing) return;
        string hex = TxtCustomHex.Text.Trim();
        if (hex.StartsWith("#") && (hex.Length == 7 || hex.Length == 9))
        {
            SettingsService.Current.Appearance.CustomThemeColorHex = hex;
            SettingsService.NotifyChanged();
        }
    }
    #endregion

    #region Avatar Event Handlers
    private void ChkAvatarEnabled_Click(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        SettingsService.Current.Avatar.AvatarEnabled = ChkAvatarEnabled.IsChecked ?? true;
        SettingsService.NotifyChanged();
    }

    private void SliderAvatarSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isInitializing) return;
        TxtAvatarSizeVal.Text = $"%{(int)e.NewValue}";
        SettingsService.Current.Avatar.AvatarSize = e.NewValue;
        SettingsService.NotifyChanged();
    }

    private void ChkAvatarIdle_Click(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        SettingsService.Current.Avatar.IdleAnimation = ChkAvatarIdle.IsChecked ?? true;
        SettingsService.NotifyChanged();
    }

    private void ChkAvatarReactions_Click(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        SettingsService.Current.Avatar.ReactionAnimations = ChkAvatarReactions.IsChecked ?? true;
        SettingsService.NotifyChanged();
    }

    private void ChkAvatarGlow_Click(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        SettingsService.Current.Avatar.AvatarGlow = ChkAvatarGlow.IsChecked ?? true;
        SettingsService.NotifyChanged();
    }

    private void AvatarSpeed_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string speed)
        {
            SelectSegment(new[] { BtnAvatarSpeedSlow, BtnAvatarSpeedNormal, BtnAvatarSpeedFast }, speed);
            SettingsService.Current.Avatar.AvatarAnimationSpeed = speed;
            SettingsService.NotifyChanged();
        }
    }
    #endregion

    #region Mini Mode Event Handlers
    private void ChkMiniModeEnabled_Click(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        SettingsService.Current.MiniMode.MiniModeEnabled = ChkMiniModeEnabled.IsChecked ?? true;
        SettingsService.NotifyChanged();
    }

    private void ChkRightClickMini_Click(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        SettingsService.Current.MiniMode.RightClickMiniMode = ChkRightClickMini.IsChecked ?? true;
        SettingsService.NotifyChanged();
    }

    private void SliderBubbleSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isInitializing) return;
        TxtBubbleSizeVal.Text = $"{(int)e.NewValue} px";
        SettingsService.Current.MiniMode.BubbleSize = e.NewValue;
        SettingsService.NotifyChanged();
    }

    private void BubblePos_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string pos)
        {
            SelectSegment(new[] { BtnPosTopRight, BtnPosTopLeft }, pos);
            SettingsService.Current.MiniMode.BubblePosition = pos;
            SettingsService.NotifyChanged();
        }
    }

    private void MiniSpeed_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string speed)
        {
            SelectSegment(new[] { BtnMiniSpeedSlow, BtnMiniSpeedNormal, BtnMiniSpeedFast }, speed);
            SettingsService.Current.MiniMode.TransitionSpeed = speed;
            SettingsService.NotifyChanged();
        }
    }

    private void ChkFluidMorph_Click(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        SettingsService.Current.MiniMode.FluidMorph = ChkFluidMorph.IsChecked ?? true;
        SettingsService.NotifyChanged();
    }
    #endregion

    #region Behavior Event Handlers
    private void ChkStartWithWindows_Click(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        bool enable = ChkStartWithWindows.IsChecked ?? false;
        bool actual = StartupService.SetStartup(enable);
        SettingsService.Current.Behavior.StartWithWindows = actual;
        ChkStartWithWindows.IsChecked = actual;
        SettingsService.NotifyChanged();
    }

    private void ChkShowNotifications_Click(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        SettingsService.Current.Behavior.ShowNotifications = ChkShowNotifications.IsChecked ?? true;
        SettingsService.NotifyChanged();
    }

    private void ChkMusicReactions_Click(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        SettingsService.Current.Behavior.MusicReactions = ChkMusicReactions.IsChecked ?? true;
        SettingsService.NotifyChanged();
    }

    private void ChkBatteryReactions_Click(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        SettingsService.Current.Behavior.BatteryReactions = ChkBatteryReactions.IsChecked ?? true;
        SettingsService.NotifyChanged();
    }

    private void ChkVolumeReactions_Click(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        SettingsService.Current.Behavior.VolumeReactions = ChkVolumeReactions.IsChecked ?? true;
        SettingsService.NotifyChanged();
    }

    private void ChkFullscreenAutoHide_Click(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        SettingsService.Current.Behavior.FullscreenAutoHide = ChkFullscreenAutoHide.IsChecked ?? true;
        SettingsService.NotifyChanged();
    }
    #endregion

    #region Performance Event Handlers
    private void PerfMode_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string mode)
        {
            SelectSegment(new[] { BtnPerfLow, BtnPerfBalanced, BtnPerfHigh }, mode);
            SettingsService.Current.Performance.PerformanceMode = mode;
            UpdatePerfDescription(mode);
            SettingsService.NotifyChanged();
        }
    }

    private void UpdatePerfDescription(string mode)
    {
        TxtPerfDescription.Text = mode switch
        {
            "Low" => "Low: Daha düşük CPU/GPU kullanımı. Polling sıklığı azaltılır.",
            "High" => "High: Daha akıcı animasyonlar (144Hz) ve daha fazla görsel efekt.",
            _ => "Balanced: Görsel kalite ve performans dengesi."
        };
    }

    private void ChkReduceMotion_Click(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        SettingsService.Current.Performance.ReduceMotion = ChkReduceMotion.IsChecked ?? false;
        SettingsService.NotifyChanged();
    }
    #endregion

    #region Reset Defaults
    private void BtnResetDefaults_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Tüm ayarları varsayılan fabrika değerlerine sıfırlamak istiyor musunuz?",
            "Ayarları Sıfırla",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _isInitializing = true;
            SettingsService.ResetToDefaults();
            LoadSettingsIntoUi();
            _isInitializing = false;
        }
    }
    #endregion
}
