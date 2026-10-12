using System.Diagnostics;
using System.IO;

namespace DynamicIslandWin.Services;

public static class StartupService
{
    private static string StartupShortcutPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Startup),
        "DynamicIslandWin.lnk");

    public static bool IsStartupEnabled()
    {
        return File.Exists(StartupShortcutPath);
    }

    public static bool ToggleStartup()
    {
        try
        {
            if (IsStartupEnabled())
            {
                File.Delete(StartupShortcutPath);
                return false;
            }

            string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
            if (string.IsNullOrWhiteSpace(exePath)) return false;

            string workDir = Path.GetDirectoryName(exePath) ?? "";
            string psScript = $"$wsh = New-Object -ComObject WScript.Shell; $s = $wsh.CreateShortcut('{StartupShortcutPath}'); $s.TargetPath = '{exePath}'; $s.WorkingDirectory = '{workDir}'; $s.Save()";

            var psi = new ProcessStartInfo("powershell.exe", $"-NoProfile -WindowStyle Hidden -Command \"{psScript}\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit(3000);

            return File.Exists(StartupShortcutPath);
        }
        catch
        {
            return IsStartupEnabled();
        }
    }

    public static bool SetStartup(bool enable)
    {
        try
        {
            if (!enable)
            {
                if (File.Exists(StartupShortcutPath))
                {
                    File.Delete(StartupShortcutPath);
                }
                return false;
            }

            if (File.Exists(StartupShortcutPath)) return true;

            string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
            if (string.IsNullOrWhiteSpace(exePath)) return false;

            string workDir = Path.GetDirectoryName(exePath) ?? "";
            string psScript = $"$wsh = New-Object -ComObject WScript.Shell; $s = $wsh.CreateShortcut('{StartupShortcutPath}'); $s.TargetPath = '{exePath}'; $s.WorkingDirectory = '{workDir}'; $s.Save()";

            var psi = new ProcessStartInfo("powershell.exe", $"-NoProfile -WindowStyle Hidden -Command \"{psScript}\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit(3000);

            return File.Exists(StartupShortcutPath);
        }
        catch
        {
            return IsStartupEnabled();
        }
    }
}
