using System.Runtime.InteropServices;
using Windows.Devices.Enumeration;
using Windows.Media.Devices;

namespace DynamicIslandWin.Services;

public sealed class AudioOutputDevice
{
    public string FullWinRtId { get; init; } = "";
    public string CoreAudioId { get; init; } = "";
    public string Name { get; init; } = "";
    public bool IsDefault { get; init; }
}

public static class AudioService
{
    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumeratorComObject
    {
    }

    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(int dataFlow, int dwStateMask, out IntPtr ppDevices);
        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppEndpoint);
    }

    [Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
    }

    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        int RegisterControlChangeNotify(IntPtr pNotify);
        int UnregisterControlChangeNotify(IntPtr pNotify);
        int GetChannelCount(out int pnChannelCount);
        int SetMasterVolumeLevel(float fLevelDB, ref Guid pguidEventContext);
        int SetMasterVolumeLevelScalar(float fLevel, ref Guid pguidEventContext);
        int GetMasterVolumeLevel(out float pfLevelDB);
        int GetMasterVolumeLevelScalar(out float pfLevel);
        int SetChannelVolumeLevel(uint nChannel, float fLevelDB, ref Guid pguidEventContext);
        int SetChannelVolumeLevelScalar(uint nChannel, float fLevel, ref Guid pguidEventContext);
        int GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);
        int GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);
        int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, ref Guid pguidEventContext);
        int GetMute([MarshalAs(UnmanagedType.Bool)] out bool pbMute);
    }

    [ComImport]
    [Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9")]
    private class PolicyConfigClientComObject
    {
    }

    [Guid("f8679f50-850a-41cf-9c72-430f290290c8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPolicyConfig
    {
        int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr ppFormat);
        int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bDefault, IntPtr ppFormat);
        int ResetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName);
        int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr pEndpointFormat, IntPtr mixFormat);
        int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bDefault, IntPtr pmftDefaultPeriod, IntPtr pmftMinimumPeriod);
        int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr pmftPeriod);
        int GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr pMode);
        int SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr mode);
        int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bFxStore, IntPtr key, IntPtr pv);
        int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bFxStore, IntPtr key, IntPtr pv);
        int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int role);
        int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bVisible);
    }

    private static IAudioEndpointVolume? GetVolumeObject()
    {
        try
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            int hr = enumerator.GetDefaultAudioEndpoint(0, 1, out IMMDevice device);
            if (hr != 0 || device == null) return null;

            Guid iid = typeof(IAudioEndpointVolume).GUID;
            hr = device.Activate(ref iid, 23, IntPtr.Zero, out object volObj);
            if (hr != 0) return null;

            return volObj as IAudioEndpointVolume;
        }
        catch
        {
            return null;
        }
    }

    public static (int VolumePercent, bool IsMuted) GetVolumeState()
    {
        try
        {
            var vol = GetVolumeObject();
            if (vol == null) return (50, false);

            vol.GetMasterVolumeLevelScalar(out float level);
            vol.GetMute(out bool muted);
            int pct = (int)Math.Round(Math.Clamp(level * 100f, 0f, 100f));
            return (pct, muted);
        }
        catch
        {
            return (50, false);
        }
    }

    public static void SetVolume(int percent)
    {
        try
        {
            var vol = GetVolumeObject();
            if (vol == null) return;

            float scalar = Math.Clamp(percent / 100f, 0f, 1f);
            Guid ctx = Guid.Empty;
            vol.SetMasterVolumeLevelScalar(scalar, ref ctx);
            if (percent > 0)
            {
                vol.GetMute(out bool isMuted);
                if (isMuted)
                {
                    vol.SetMute(false, ref ctx);
                }
            }
        }
        catch
        {
        }
    }

    public static bool ToggleMute()
    {
        try
        {
            var vol = GetVolumeObject();
            if (vol == null) return false;

            vol.GetMute(out bool isMuted);
            Guid ctx = Guid.Empty;
            bool nextState = !isMuted;
            vol.SetMute(nextState, ref ctx);
            return nextState;
        }
        catch
        {
            return false;
        }
    }

    private static IAudioEndpointVolume? GetMicVolumeObject(int role = 0)
    {
        try
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            // dataFlow = 1 (eCapture / Microphone)
            int hr = enumerator.GetDefaultAudioEndpoint(1, role, out IMMDevice device);
            if (hr != 0 || device == null) return null;

            Guid iid = typeof(IAudioEndpointVolume).GUID;
            hr = device.Activate(ref iid, 23, IntPtr.Zero, out object volObj);
            if (hr != 0) return null;

            return volObj as IAudioEndpointVolume;
        }
        catch
        {
            return null;
        }
    }

    public static (bool HasMic, bool IsMuted) GetMicMuteState()
    {
        try
        {
            var mic = GetMicVolumeObject(0) ?? GetMicVolumeObject(2);
            if (mic == null) return (false, false);

            mic.GetMute(out bool muted);
            return (true, muted);
        }
        catch
        {
            return (false, false);
        }
    }

    public static bool ToggleMicMute()
    {
        try
        {
            var mic0 = GetMicVolumeObject(0);
            if (mic0 == null) return false;

            mic0.GetMute(out bool isMuted);
            bool nextState = !isMuted;
            Guid ctx = Guid.Empty;
            mic0.SetMute(nextState, ref ctx);

            // Also sync communications endpoint (Discord / Games)
            var micComm = GetMicVolumeObject(2);
            micComm?.SetMute(nextState, ref ctx);

            return nextState;
        }
        catch
        {
            return false;
        }
    }

    public static async Task<List<AudioOutputDevice>> GetOutputDevicesAsync()
    {
        var result = new List<AudioOutputDevice>();
        try
        {
            string defaultId = MediaDevice.GetDefaultAudioRenderId(AudioDeviceRole.Default) ?? "";
            string selector = MediaDevice.GetAudioRenderSelector();
            var devices = await DeviceInformation.FindAllAsync(selector);

            foreach (var d in devices)
            {
                if (!d.IsEnabled) continue;
                string coreId = ExtractCoreAudioEndpointId(d.Id);
                result.Add(new AudioOutputDevice
                {
                    FullWinRtId = d.Id,
                    CoreAudioId = coreId,
                    Name = d.Name,
                    IsDefault = string.Equals(d.Id, defaultId, StringComparison.OrdinalIgnoreCase)
                });
            }
        }
        catch
        {
        }
        return result;
    }

    public static async Task<string> CycleNextOutputDeviceAsync()
    {
        try
        {
            var devices = await GetOutputDevicesAsync();
            if (devices.Count == 0) return "Ses Cihazı Yok";
            if (devices.Count == 1) return devices[0].Name;

            int currentIdx = devices.FindIndex(d => d.IsDefault);
            int nextIdx = (currentIdx + 1) % devices.Count;
            var target = devices[nextIdx];

            if (!string.IsNullOrWhiteSpace(target.CoreAudioId))
            {
                var policy = (IPolicyConfig)new PolicyConfigClientComObject();
                policy.SetDefaultEndpoint(target.CoreAudioId, 0); // eConsole
                policy.SetDefaultEndpoint(target.CoreAudioId, 1); // eMultimedia
                policy.SetDefaultEndpoint(target.CoreAudioId, 2); // eCommunications
            }

            return target.Name;
        }
        catch
        {
            return "Varsayılan Hoparlör";
        }
    }

    private static string ExtractCoreAudioEndpointId(string winRtId)
    {
        // Format: \\?\SWD#MMDEVAPI#{0.0.0.00000000}.{guid}#{...}
        int idx = winRtId.IndexOf("{0.0.0.00000000}.", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return "";
        int endHash = winRtId.IndexOf('#', idx);
        return endHash > idx ? winRtId[idx..endHash] : winRtId[idx..];
    }
}
