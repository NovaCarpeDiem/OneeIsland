using System.IO;
using System.Text.Json;

namespace DynamicIslandWin.Services;

public sealed class AppearanceSettings
{
    public double IslandGlow { get; set; } = 70.0; // 0–100
    public double IslandOpacity { get; set; } = 100.0; // 0–100
    public string AnimationSpeed { get; set; } = "Normal"; // "Slow", "Normal", "Fast"
    public bool AdaptiveGlow { get; set; } = true;
    public string Theme { get; set; } = "Adaptive"; // "Adaptive", "Yellow", "Custom"
    public string CustomThemeColorHex { get; set; } = "#F43F5E";
}

public sealed class AvatarSettings
{
    public bool AvatarEnabled { get; set; } = true;
    public double AvatarSize { get; set; } = 100.0; // 70–140%
    public bool IdleAnimation { get; set; } = true;
    public bool ReactionAnimations { get; set; } = true;
    public bool AvatarGlow { get; set; } = true;
    public string AvatarAnimationSpeed { get; set; } = "Normal"; // "Slow", "Normal", "Fast"
}

public sealed class MiniModeSettings
{
    public bool MiniModeEnabled { get; set; } = true;
    public bool RightClickMiniMode { get; set; } = true;
    public double BubbleSize { get; set; } = 58.0; // 48–76 px
    public string BubblePosition { get; set; } = "Top Right"; // "Top Right", "Top Left"
    public string TransitionSpeed { get; set; } = "Normal"; // "Slow", "Normal", "Fast"
    public bool FluidMorph { get; set; } = true;
}

public sealed class BehaviorSettings
{
    public bool StartWithWindows { get; set; } = false;
    public bool ShowNotifications { get; set; } = true;
    public bool MusicReactions { get; set; } = true;
    public bool BatteryReactions { get; set; } = true;
    public bool VolumeReactions { get; set; } = true;
    public bool FullscreenAutoHide { get; set; } = true;
}

public sealed class PerformanceSettings
{
    public string PerformanceMode { get; set; } = "Balanced"; // "Low", "Balanced", "High"
    public bool ReduceMotion { get; set; } = false;
}

public sealed class AppSettings
{
    public AppearanceSettings Appearance { get; set; } = new();
    public AvatarSettings Avatar { get; set; } = new();
    public MiniModeSettings MiniMode { get; set; } = new();
    public BehaviorSettings Behavior { get; set; } = new();
    public PerformanceSettings Performance { get; set; } = new();
}

public static class SettingsService
{
    private static readonly string AppFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DynamicIslandWin");

    private static readonly string SettingsFilePath = Path.Combine(AppFolder, "settings.json");

    private static AppSettings _current = new();
    public static AppSettings Current => _current;

    public static event Action<AppSettings>? SettingsChanged;

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                string json = File.ReadAllText(SettingsFilePath, System.Text.Encoding.UTF8);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (loaded != null)
                {
                    _current = loaded;
                    _current.Appearance ??= new();
                    _current.Avatar ??= new();
                    _current.MiniMode ??= new();
                    _current.Behavior ??= new();
                    _current.Performance ??= new();
                    
                    // Sync start with windows from actual OS state if needed
                    _current.Behavior.StartWithWindows = StartupService.IsStartupEnabled();
                    return _current;
                }
            }
        }
        catch
        {
        }

        _current = new AppSettings();
        _current.Behavior.StartWithWindows = StartupService.IsStartupEnabled();
        Save();
        return _current;
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(AppFolder);
            string json = JsonSerializer.Serialize(_current, JsonOptions);
            File.WriteAllText(SettingsFilePath, json, System.Text.Encoding.UTF8);
        }
        catch
        {
        }
    }

    public static void NotifyChanged()
    {
        Save();
        SettingsChanged?.Invoke(_current);
    }

    public static void ResetToDefaults()
    {
        _current = new AppSettings();
        _current.Behavior.StartWithWindows = false;
        StartupService.SetStartup(false);
        NotifyChanged();
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };
}
