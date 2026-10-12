using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Win32;

namespace DynamicIslandWin.Services;

public class PrivacyStatusEventArgs : EventArgs
{
    public bool IsCameraActive { get; set; }
    public string CameraApp { get; set; } = string.Empty;
    public bool IsMicrophoneActive { get; set; }
    public string MicrophoneApp { get; set; } = string.Empty;
}

/// <summary>
/// Monitors Windows 10/11 Camera and Microphone hardware usage via CapabilityAccessManager registry.
/// Provides real-time notifications for Apple-style green/orange privacy indicator dots.
/// </summary>
public class PrivacyIndicatorService : IDisposable
{
    private const string MicKeyPath = @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone";
    private const string CamKeyPath = @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\webcam";

    private Timer? _pollTimer;
    private bool _isDisposed;
    private readonly object _lock = new();

    public bool IsCameraActive { get; private set; }
    public string ActiveCameraApp { get; private set; } = string.Empty;
    public bool IsMicrophoneActive { get; private set; }
    public string ActiveMicrophoneApp { get; private set; } = string.Empty;

    public event EventHandler<PrivacyStatusEventArgs>? StatusChanged;

    public PrivacyIndicatorService()
    {
        // Poll every 1.5 seconds in background
        _pollTimer = new Timer(_ => CheckHardwareUsage(), null, 500, 1500);
    }

    public void CheckHardwareUsage()
    {
        if (_isDisposed) return;

        bool camActive = false;
        string camApp = string.Empty;

        bool micActive = false;
        string micApp = string.Empty;

        try
        {
            camActive = CheckCapability(CamKeyPath, out camApp);
            micActive = CheckCapability(MicKeyPath, out micApp);
        }
        catch
        {
            // Suppress any registry access quirks
        }

        bool changed = false;
        lock (_lock)
        {
            if (camActive != IsCameraActive || micActive != IsMicrophoneActive ||
                camApp != ActiveCameraApp || micApp != ActiveMicrophoneApp)
            {
                IsCameraActive = camActive;
                ActiveCameraApp = camApp;
                IsMicrophoneActive = micActive;
                ActiveMicrophoneApp = micApp;
                changed = true;
            }
        }

        if (changed)
        {
            try
            {
                StatusChanged?.Invoke(this, new PrivacyStatusEventArgs
                {
                    IsCameraActive = camActive,
                    CameraApp = camApp,
                    IsMicrophoneActive = micActive,
                    MicrophoneApp = micApp
                });
            }
            catch { }
        }
    }

    private static bool CheckCapability(string consentStoreSubKey, out string activeAppName)
    {
        activeAppName = string.Empty;

        try
        {
            using var baseKey = Registry.CurrentUser.OpenSubKey(consentStoreSubKey);
            if (baseKey == null) return false;

            // 1. Check packaged UWP apps directly under the key
            foreach (string subKeyName in baseKey.GetSubKeyNames())
            {
                if (subKeyName.Equals("NonPackaged", StringComparison.OrdinalIgnoreCase))
                    continue;

                using var subKey = baseKey.OpenSubKey(subKeyName);
                if (subKey != null && IsDeviceInActiveUse(subKey))
                {
                    activeAppName = CleanPackagedAppName(subKeyName);
                    return true;
                }
            }

            // 2. Check Win32 desktop apps under "NonPackaged"
            using var nonPackagedKey = baseKey.OpenSubKey("NonPackaged");
            if (nonPackagedKey != null)
            {
                foreach (string subKeyName in nonPackagedKey.GetSubKeyNames())
                {
                    using var subKey = nonPackagedKey.OpenSubKey(subKeyName);
                    if (subKey != null && IsDeviceInActiveUse(subKey))
                    {
                        activeAppName = CleanNonPackagedAppName(subKeyName);
                        return true;
                    }
                }
            }
        }
        catch
        {
        }

        return false;
    }

    private static bool IsDeviceInActiveUse(RegistryKey key)
    {
        try
        {
            object? startObj = key.GetValue("LastUsedTimeStart");
            object? stopObj = key.GetValue("LastUsedTimeStop");

            if (startObj is long startTime && startTime > 0)
            {
                if (stopObj is long stopTime)
                {
                    // If stop is 0, the device stream is open right now
                    // Or if start is greater than stop, device is currently actively recording
                    return stopTime == 0 || startTime > stopTime;
                }
            }
        }
        catch
        {
        }

        return false;
    }

    private static string CleanPackagedAppName(string rawKey)
    {
        try
        {
            // Example: "5319275A.WhatsAppDesktop_cv1g1gvanyjgm" -> "WhatsApp"
            string name = rawKey;
            int dotIdx = name.IndexOf('.');
            if (dotIdx >= 0 && dotIdx < name.Length - 1)
            {
                name = name.Substring(dotIdx + 1);
            }
            int underIdx = name.IndexOf('_');
            if (underIdx > 0)
            {
                name = name.Substring(0, underIdx);
            }
            name = Regex.Replace(name, "Desktop$", "", RegexOptions.IgnoreCase);
            return string.IsNullOrWhiteSpace(name) ? rawKey : name;
        }
        catch
        {
            return rawKey;
        }
    }

    private static string CleanNonPackagedAppName(string rawKey)
    {
        try
        {
            // Example: "C:#Program Files#Google#Chrome#Application#chrome.exe"
            string path = rawKey.Replace('#', Path.DirectorySeparatorChar);
            string exeName = Path.GetFileNameWithoutExtension(path);
            if (!string.IsNullOrWhiteSpace(exeName))
            {
                // Capitalize first letter
                if (exeName.Length > 1)
                    return char.ToUpperInvariant(exeName[0]) + exeName.Substring(1);
                return exeName.ToUpperInvariant();
            }
        }
        catch
        {
        }

        return rawKey;
    }

    public void Dispose()
    {
        _isDisposed = true;
        _pollTimer?.Dispose();
        _pollTimer = null;
    }
}
