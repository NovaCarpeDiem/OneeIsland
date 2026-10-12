using System.Globalization;

namespace DynamicIslandWin.Services;

public static class LocalizationService
{
    private static string _currentLanguage = "tr";

    public static string CurrentLanguage
    {
        get => _currentLanguage;
        set
        {
            var clean = (value ?? "tr").ToLowerInvariant();
            if (clean != "tr" && clean != "en") clean = "tr";
            if (_currentLanguage != clean)
            {
                _currentLanguage = clean;
                LanguageChanged?.Invoke();
            }
        }
    }

    public static CultureInfo Culture => _currentLanguage == "tr"
        ? CultureInfo.GetCultureInfo("tr-TR")
        : CultureInfo.GetCultureInfo("en-US");

    public static event Action? LanguageChanged;

    private static readonly Dictionary<string, string> StringsTr = new(StringComparer.OrdinalIgnoreCase)
    {
        // Tabs
        ["TabMedia"] = "Medya",
        ["TabTray"] = "Tepsi",
        ["TabSystem"] = "Sistem",
        ["TabTimer"] = "Sayaç",
        ["TabDock"] = "Dock",

        // Tooltips & Action Buttons
        ["TooltipSearch"] = "Spotlight Hızlı Başlatıcı (Alt + Space)",
        ["TooltipMic"] = "Gizlilik: Mikrofon Dinleniyor",
        ["TooltipCam"] = "Gizlilik: Kamera Açık",
        ["TooltipFullScreenshot"] = "Tam Ekran Görüntüsü Al (Tepsiye & Panoya Kaydeder)",
        ["TooltipRegionSnip"] = "Bölge Kırpma Aracı (Seçilen Alanı Tepsiye Ekler)",
        ["TooltipPin"] = "Adayı Sabitle (Açık Tut)",
        ["TooltipCollapse"] = "Adayı Küçült (Kompakt Hap Modu)",
        ["TooltipSettings"] = "Ayarları Aç",

        // Media & Lyrics
        ["NoLyrics"] = "Şarkı sözü bulunamadı",
        ["LyricsSearch"] = "Şarkı sözü aranıyor...",
        ["NoMedia"] = "Müzik Çalmıyor",
        ["SpotifyPlaying"] = "Spotify Çalıyor",

        // Toasts & Notifications
        ["FocusStartedTitle"] = "Odaklanma Başladı",
        ["FocusStartedDesc"] = "Pomodoro odak modu aktif.",
        ["FocusEndedTitle"] = "Pomodoro Tamamlandı!",
        ["FocusEndedDesc"] = "Tebrikler! +50 Odak XP kazandın.",
        ["SnipCopied"] = "Ekran görüntüsü panoya kopyalandı.",
        ["LowBatteryTitle"] = "Düşük Pil Uyarısı",
        ["LowBatteryDesc"] = "Pil seviyesi %20'nin altına indi. Lütfen şarja takın.",

        // Tray Context Menu
        ["TrayToggle"] = "Adayı Genişlet / Daralt",
        ["TrayPin"] = "Adayı Sabitle (Açık Tut)",
        ["TraySnap"] = "Ortaya Hizala",
        ["TrayReset"] = "Konumu Sıfırla",
        ["TraySettings"] = "⚙️ Ayarlar (Settings)",
        ["TrayCoffee"] = "☕ Geliştiriciye Kahve Ismarla (Buy Me a Coffee)",
        ["TrayExit"] = "Çıkış",

        // Settings Window Headers & Tabs
        ["SettingsTitle"] = "Dynamic Island • Ayarlar",
        ["NavAppearance"] = "Görünüm",
        ["NavAvatar"] = "Avatar",
        ["NavMini"] = "Mini Mod",
        ["NavBehavior"] = "Davranış",
        ["NavPerf"] = "Performans",

        // Settings Language Card
        ["LangCardTitle"] = "Dil / Language",
        ["LangCardDesc"] = "Arayüz, menüler ve bildirim dili",

        // Settings Appearance
        ["IslandGlow"] = "Ada Parıltısı",
        ["IslandGlowDesc"] = "Arka plan aurası ve kenarlık neon parlaması",
        ["IslandOpacity"] = "Ada Saydamlığı",
        ["IslandOpacityDesc"] = "Ada arka planının opaklık düzeyi",
        ["AnimSpeed"] = "Animasyon Hızı",
        ["AnimSpeedDesc"] = "Açılış, kapanış ve yay fiziği tepki süresi",
        ["AdaptiveGlow"] = "Adaptif Renk Işıması",
        ["AdaptiveGlowDesc"] = "Çalan şarkı albüm kapağının rengine göre neon parlama",
        ["ThemeStyle"] = "Tema Stili",
        ["ThemeStyleDesc"] = "Ada ana vurgu ve neon ışık rengi",

        // Settings Avatar
        ["AvatarEnabled"] = "Avatar Aktif",
        ["AvatarEnabledDesc"] = "Ada üzerinde Onee karakterini gösterir / gizler",
        ["AvatarSize"] = "Avatar Boyutu",
        ["AvatarIdle"] = "Boşta Canlı Animasyon",
        ["AvatarIdleDesc"] = "Nefes alma, göz kırpma ve meraklı bakışlar",
        ["AvatarReactions"] = "Dürtme & Etkileşim Tepkileri",
        ["AvatarReactionsDesc"] = "Tıklama ve sevme durumunda kalp parçacıkları",
        ["AvatarGlow"] = "Avatar Çevre Işıması",
        ["AvatarGlowDesc"] = "Avatar arkasındaki parlayan neon halkası",
        ["AvatarSpeed"] = "Avatar Hareket Hızı",

        // Settings Mini Mode
        ["MiniModeEnabled"] = "Mini Mod Aktif",
        ["MiniModeEnabledDesc"] = "Adayı mini avatar baloncuk moduna küçültme yeteneği",
        ["RightClickMini"] = "Sağ Tık ile Mini Moda Geçiş",
        ["RightClickMiniDesc"] = "Adaya sağ tıklandığında mini baloncuk moduna morph olur",
        ["BubbleSize"] = "Baloncuk Boyutu",
        ["BubblePos"] = "Varsayılan Köşe",
        ["FluidMorph"] = "Akıcı Şekil Değiştirme (Fluid Morph)",

        // Settings Behavior
        ["StartWithWindows"] = "Windows ile Birlikte Başlat",
        ["StartWithWindowsDesc"] = "Bilgisayar açıldığında adayı otomatik olarak başlatır",
        ["ShowNotifs"] = "Sistem Bildirimleri",
        ["ShowNotifsDesc"] = "Ses, şarj ve sayaç bildirimlerini adada göster",
        ["MusicReactions"] = "Müzik Ritmi & Kulaklık",
        ["MusicReactionsDesc"] = "Müzik çalarken avatara kulaklık takılır ve ritme uyar",
        ["BatteryReactions"] = "Pil Uyarıları & Şarj Parıltısı",
        ["FullscreenAutoHide"] = "Tam Ekranda Otomatik Gizle",
        ["FullscreenAutoHideDesc"] = "Tam ekran oyun veya video açıkken adayı gizler",

        // Settings Performance
        ["PerfProfile"] = "Performans Profili",
        ["PerfDesc"] = "Görsel kalite ve sistem performansı dengesi.",
        ["ReduceMotion"] = "Hareketi Azalt (Reduce Motion)",
        ["ReduceMotionDesc"] = "Düşük donanımlı bilgisayarlar için animasyonları sadeleştirir",

        // Common Segment Options
        ["Slow"] = "Yavaş",
        ["Normal"] = "Normal",
        ["Fast"] = "Hızlı",
        ["Low"] = "Düşük",
        ["Balanced"] = "Dengeli",
        ["High"] = "Yüksek",
        ["TopRight"] = "Sağ Üst",
        ["TopLeft"] = "Sol Üst",
        ["Adaptive"] = "Adaptif",
        ["Custom"] = "Özel"
    };

    private static readonly Dictionary<string, string> StringsEn = new(StringComparer.OrdinalIgnoreCase)
    {
        // Tabs
        ["TabMedia"] = "Media",
        ["TabTray"] = "Tray",
        ["TabSystem"] = "System",
        ["TabTimer"] = "Timer",
        ["TabDock"] = "Dock",

        // Tooltips & Action Buttons
        ["TooltipSearch"] = "Spotlight Quick Launcher (Alt + Space)",
        ["TooltipMic"] = "Privacy: Microphone Active",
        ["TooltipCam"] = "Privacy: Camera Active",
        ["TooltipFullScreenshot"] = "Take Full Screenshot (Saves to Tray & Clipboard)",
        ["TooltipRegionSnip"] = "Region Snipping Tool (Adds Selection to Tray)",
        ["TooltipPin"] = "Pin Island (Keep Open)",
        ["TooltipCollapse"] = "Collapse Island (Compact Pill Mode)",
        ["TooltipSettings"] = "Open Settings",

        // Media & Lyrics
        ["NoLyrics"] = "No lyrics available",
        ["LyricsSearch"] = "Searching lyrics...",
        ["NoMedia"] = "No Media Playing",
        ["SpotifyPlaying"] = "Spotify Playing",

        // Toasts & Notifications
        ["FocusStartedTitle"] = "Focus Started",
        ["FocusStartedDesc"] = "Pomodoro focus session active.",
        ["FocusEndedTitle"] = "Focus Session Completed!",
        ["FocusEndedDesc"] = "Congratulations! You earned +50 Focus XP.",
        ["SnipCopied"] = "Screenshot copied to clipboard.",
        ["LowBatteryTitle"] = "Low Battery Warning",
        ["LowBatteryDesc"] = "Battery dropped below 20%. Please plug in charger.",

        // Tray Context Menu
        ["TrayToggle"] = "Expand / Collapse Island",
        ["TrayPin"] = "Pin Island (Keep Open)",
        ["TraySnap"] = "Snap to Center",
        ["TrayReset"] = "Reset Position",
        ["TraySettings"] = "⚙️ Settings",
        ["TrayCoffee"] = "☕ Buy Me a Coffee",
        ["TrayExit"] = "Exit",

        // Settings Window Headers & Tabs
        ["SettingsTitle"] = "Dynamic Island • Settings",
        ["NavAppearance"] = "Appearance",
        ["NavAvatar"] = "Avatar",
        ["NavMini"] = "Mini Mode",
        ["NavBehavior"] = "Behavior",
        ["NavPerf"] = "Performance",

        // Settings Language Card
        ["LangCardTitle"] = "Language / Dil",
        ["LangCardDesc"] = "Interface, menu and notification language",

        // Settings Appearance
        ["IslandGlow"] = "Island Glow",
        ["IslandGlowDesc"] = "Background ambient aura and neon rim intensity",
        ["IslandOpacity"] = "Island Opacity",
        ["IslandOpacityDesc"] = "Background glass opacity level",
        ["AnimSpeed"] = "Animation Speed",
        ["AnimSpeedDesc"] = "Expansion, collapse and spring physics response",
        ["AdaptiveGlow"] = "Adaptive Color Glow",
        ["AdaptiveGlowDesc"] = "Dynamic neon glow extracted from active album artwork",
        ["ThemeStyle"] = "Theme Accent",
        ["ThemeStyleDesc"] = "Island primary accent and halo lighting",

        // Settings Avatar
        ["AvatarEnabled"] = "Avatar Enabled",
        ["AvatarEnabledDesc"] = "Show / hide Onee companion on the island",
        ["AvatarSize"] = "Avatar Size",
        ["AvatarIdle"] = "Idle Animation",
        ["AvatarIdleDesc"] = "Breathing, blinking, and curious glances",
        ["AvatarReactions"] = "Poke & Interaction Reactions",
        ["AvatarReactionsDesc"] = "Heart particles and joyful emotes when clicked",
        ["AvatarGlow"] = "Avatar Halo Glow",
        ["AvatarGlowDesc"] = "Glowing neon ring surrounding avatar",
        ["AvatarSpeed"] = "Avatar Motion Speed",

        // Settings Mini Mode
        ["MiniModeEnabled"] = "Mini Mode Enabled",
        ["MiniModeEnabledDesc"] = "Enable detaching island into mini floating companion orb",
        ["RightClickMini"] = "Right Click for Mini Mode",
        ["RightClickMiniDesc"] = "Right click island to detach into floating mini bubble",
        ["BubbleSize"] = "Bubble Size",
        ["BubblePos"] = "Default Corner",
        ["FluidMorph"] = "Fluid Morph Animation",

        // Settings Behavior
        ["StartWithWindows"] = "Start with Windows",
        ["StartWithWindowsDesc"] = "Automatically launch Onee Island on PC boot",
        ["ShowNotifs"] = "System Notifications",
        ["ShowNotifsDesc"] = "Display volume, battery, and timer alerts on island",
        ["MusicReactions"] = "Music Rhythm & Headphones",
        ["MusicReactionsDesc"] = "Put DJ headphones on Onee and groove to playing tunes",
        ["BatteryReactions"] = "Battery Alerts & Charging Pulse",
        ["FullscreenAutoHide"] = "Auto-Hide in Fullscreen",
        ["FullscreenAutoHideDesc"] = "Hide island during fullscreen gaming or movies",

        // Settings Performance
        ["PerfProfile"] = "Performance Profile",
        ["PerfDesc"] = "Balance between visual fidelity and system resources.",
        ["ReduceMotion"] = "Reduce Motion",
        ["ReduceMotionDesc"] = "Simplify spring animations for low-end hardware",

        // Common Segment Options
        ["Slow"] = "Slow",
        ["Normal"] = "Normal",
        ["Fast"] = "Fast",
        ["Low"] = "Low",
        ["Balanced"] = "Balanced",
        ["High"] = "High",
        ["TopRight"] = "Top Right",
        ["TopLeft"] = "Top Left",
        ["Adaptive"] = "Adaptive",
        ["Custom"] = "Custom"
    };

    public static string T(string key)
    {
        var dict = _currentLanguage == "en" ? StringsEn : StringsTr;
        if (dict.TryGetValue(key, out var val)) return val;
        if (StringsEn.TryGetValue(key, out var fallbackEn)) return fallbackEn;
        if (StringsTr.TryGetValue(key, out var fallbackTr)) return fallbackTr;
        return key;
    }
}
