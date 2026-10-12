using System.IO;
using System.Text.Json;

namespace DynamicIslandWin.Services;

public sealed class TrayFileItem
{
    public string FullPath { get; set; } = "";
    public string FileName { get; set; } = "";
    public string ExtensionBadge { get; set; } = "FILE";
    public string SizeText { get; set; } = "";
}

public sealed class QuickLaunchItem
{
    public string Icon { get; set; } = "🚀";
    public string Name { get; set; } = "";
    public string Target { get; set; } = "";
}

public sealed class AppSavedState
{
    public List<TrayFileItem> TrayFiles { get; set; } = new();
    public List<string> ClipboardHistory { get; set; } = new();
    public List<QuickLaunchItem> QuickLaunchApps { get; set; } = new();
    public string QuickNote { get; set; } = "";
    public bool IsPinned { get; set; }
    public bool SoundEffectsEnabled { get; set; } = true;
    public int NotificationSoundTheme { get; set; } = 0; // 0: Apple iOS, 1: Waterdrop, 2: Crystal, 3: Windows, 4: Mute
    public double? CustomLeftX { get; set; }
    public bool IsStealthBarMode { get; set; }
    public bool AutoHideFullscreen { get; set; } = true;
    public int ScreenBrightness { get; set; } = 100;
    public bool WarmNightMode { get; set; }
    public string? CustomWeatherCity { get; set; }
    public double? CustomWeatherLat { get; set; }
    public double? CustomWeatherLon { get; set; }
    public int SelectedTab { get; set; } = 0;
    public bool IsInAvatarMiniMode { get; set; }
}

public static class StorageService
{
    private static readonly string AppFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DynamicIslandWin");

    private static readonly string StateFilePath = Path.Combine(AppFolder, "state.json");

    public static AppSavedState Load()
    {
        try
        {
            if (!File.Exists(StateFilePath))
            {
                var def = new AppSavedState
                {
                    QuickLaunchApps = GetDefaultQuickApps()
                };
                Save(def);
                return def;
            }

            string json = File.ReadAllText(StateFilePath, System.Text.Encoding.UTF8);
            var state = JsonSerializer.Deserialize<AppSavedState>(json);
            if (state == null) return new AppSavedState { QuickLaunchApps = GetDefaultQuickApps() };

            state.TrayFiles = state.TrayFiles
                .Where(f => !string.IsNullOrWhiteSpace(f.FullPath) && (File.Exists(f.FullPath) || Directory.Exists(f.FullPath)))
                .ToList();

            if (state.QuickLaunchApps == null || state.QuickLaunchApps.Count == 0 ||
                state.QuickLaunchApps.Any(a => a.Name.Contains('\ufffd') || a.Icon.Contains('?')))
            {
                state.QuickLaunchApps = GetDefaultQuickApps();
                Save(state);
            }

            return state;
        }
        catch
        {
            return new AppSavedState { QuickLaunchApps = GetDefaultQuickApps() };
        }
    }

    public static List<QuickLaunchItem> GetDefaultQuickApps() => new()
    {
        new QuickLaunchItem { Icon = "🌐", Name = "Tarayıcı", Target = "https://www.google.com" },
        new QuickLaunchItem { Icon = "🎵", Name = "Spotify", Target = "spotify:" },
        new QuickLaunchItem { Icon = "📂", Name = "Dosyalar", Target = "explorer.exe" },
        new QuickLaunchItem { Icon = "💻", Name = "Terminal", Target = "wt.exe" },
        new QuickLaunchItem { Icon = "🧮", Name = "Hesap Mak.", Target = "calc.exe" },
        new QuickLaunchItem { Icon = "📊", Name = "Görev Yön.", Target = "taskmgr.exe" },
        new QuickLaunchItem { Icon = "⚙️", Name = "Ses Ayarı", Target = "ms-settings:sound" }
    };

    public static void Save(AppSavedState state)
    {
        try
        {
            Directory.CreateDirectory(AppFolder);
            string json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(StateFilePath, json, System.Text.Encoding.UTF8);
        }
        catch
        {
        }
    }

    public static TrayFileItem? CreateTrayItem(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var info = new FileInfo(path);
                string ext = info.Extension.TrimStart('.').ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(ext)) ext = "DOSYA";
                if (ext.Length > 5) ext = ext[..5];

                return new TrayFileItem
                {
                    FullPath = info.FullName,
                    FileName = info.Name,
                    ExtensionBadge = ext,
                    SizeText = FormatBytes(info.Length)
                };
            }

            if (Directory.Exists(path))
            {
                var dir = new DirectoryInfo(path);
                return new TrayFileItem
                {
                    FullPath = dir.FullName,
                    FileName = dir.Name,
                    ExtensionBadge = "KLASÖR",
                    SizeText = "Dizin"
                };
            }
        }
        catch
        {
        }
        return null;
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        double kb = bytes / 1024.0;
        if (kb < 1024) return $"{kb:0.#} KB";
        double mb = kb / 1024.0;
        if (mb < 1024) return $"{mb:0.#} MB";
        double gb = mb / 1024.0;
        return $"{gb:0.##} GB";
    }
}
