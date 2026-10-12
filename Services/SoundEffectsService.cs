using System.IO;
using System.Media;

namespace DynamicIslandWin.Services;

public enum NotificationSoundTheme
{
    AppleDynamicChime = 0, // 🍎 iOS Dinamik Ada & Marimba
    WaterdropBubble = 1,   // 💧 Su Damlası & Pop
    CrystalBell = 2,       // 🔔 Kristal Cam Melodisi
    WindowsModern = 3,     // 💻 Windows 11 Sistem Sesi
    Mute = 4               // 🔕 Sessiz
}

public static class SoundEffectsService
{
    public static bool IsEnabled { get; set; } = true;
    public static NotificationSoundTheme CurrentTheme { get; set; } = NotificationSoundTheme.AppleDynamicChime;

    // Cached high-fidelity SoundPlayer instances (zero lag on playback)
    private static readonly Lazy<SoundPlayer> AppleChimePlayer = new(() => CreatePlayer(GenerateAppleDynamicChime()));
    private static readonly Lazy<SoundPlayer> WaterdropPlayer = new(() => CreatePlayer(GenerateWaterdrop()));
    private static readonly Lazy<SoundPlayer> CrystalBellPlayer = new(() => CreatePlayer(GenerateCrystalBell()));
    private static readonly Lazy<SoundPlayer?> WindowsPlayer = new(() => CreateWindowsPlayer());
    private static readonly Lazy<SoundPlayer> TactileTickPlayer = new(() => CreatePlayer(GenerateTactileTick()));

    public static void PlayChime()
    {
        if (!IsEnabled || CurrentTheme == NotificationSoundTheme.Mute) return;
        PlayTheme(CurrentTheme);
    }

    public static void PlayPreview(NotificationSoundTheme theme)
    {
        if (theme == NotificationSoundTheme.Mute) return;
        PlayTheme(theme);
    }

    public static void PlayExpand()
    {
        if (!IsEnabled || CurrentTheme == NotificationSoundTheme.Mute) return;
        try { TactileTickPlayer.Value.Play(); } catch { }
    }

    public static void PlayCollapse()
    {
        if (!IsEnabled || CurrentTheme == NotificationSoundTheme.Mute) return;
        try { TactileTickPlayer.Value.Play(); } catch { }
    }

    public static void PlayTick()
    {
        if (!IsEnabled || CurrentTheme == NotificationSoundTheme.Mute) return;
        try { TactileTickPlayer.Value.Play(); } catch { }
    }

    public static void PlayPoke()
    {
        if (!IsEnabled || CurrentTheme == NotificationSoundTheme.Mute) return;
        try { WaterdropPlayer.Value.Play(); } catch { }
    }

    private static void PlayTheme(NotificationSoundTheme theme)
    {
        try
        {
            switch (theme)
            {
                case NotificationSoundTheme.AppleDynamicChime:
                    AppleChimePlayer.Value.Play();
                    break;
                case NotificationSoundTheme.WaterdropBubble:
                    WaterdropPlayer.Value.Play();
                    break;
                case NotificationSoundTheme.CrystalBell:
                    CrystalBellPlayer.Value.Play();
                    break;
                case NotificationSoundTheme.WindowsModern:
                    var winPlayer = WindowsPlayer.Value;
                    if (winPlayer != null)
                    {
                        winPlayer.Play();
                    }
                    else
                    {
                        AppleChimePlayer.Value.Play();
                    }
                    break;
                default:
                    AppleChimePlayer.Value.Play();
                    break;
            }
        }
        catch
        {
        }
    }

    public static string GetThemeDisplayName(NotificationSoundTheme theme)
    {
        return theme switch
        {
            NotificationSoundTheme.AppleDynamicChime => "🍎 iOS Dinamik Ada",
            NotificationSoundTheme.WaterdropBubble => "💧 Su Damlası & Pop",
            NotificationSoundTheme.CrystalBell => "🔔 Kristal Cam Melodisi",
            NotificationSoundTheme.WindowsModern => "💻 Windows 11 Bildirim",
            NotificationSoundTheme.Mute => "🔕 Sessiz (Kapalı)",
            _ => "🍎 iOS Dinamik Ada"
        };
    }

    private static SoundPlayer CreatePlayer(byte[] wavBytes)
    {
        var ms = new MemoryStream(wavBytes);
        var player = new SoundPlayer(ms);
        player.Load();
        return player;
    }

    private static SoundPlayer? CreateWindowsPlayer()
    {
        try
        {
            string p1 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"Media\Windows Notify Messaging.wav");
            if (File.Exists(p1))
            {
                var player = new SoundPlayer(p1);
                player.Load();
                return player;
            }

            string p2 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"Media\Windows Notify.wav");
            if (File.Exists(p2))
            {
                var player = new SoundPlayer(p2);
                player.Load();
                return player;
            }
        }
        catch
        {
        }
        return null;
    }

    /// <summary>
    /// 🍎 Apple iOS Dynamic Island & Marimba Chime:
    /// Silky smooth, organic acoustic marimba & glass bell dual-note chime (E5 -> B5).
    /// </summary>
    private static byte[] GenerateAppleDynamicChime()
    {
        int sampleRate = 44100;
        double duration = 0.38;
        int numSamples = (int)(sampleRate * duration);
        short[] samples = new short[numSamples];

        for (int i = 0; i < numSamples; i++)
        {
            double t = (double)i / sampleRate;
            double total = 0.0;

            // Note 1: E5 (659.25 Hz) - Warm acoustic wood/marimba strike
            if (t < 0.28)
            {
                double attack = t < 0.005 ? (t / 0.005) : 1.0;
                double decay = Math.Exp(-13.0 * t);
                double f = 659.25;
                double n1 = Math.Sin(2 * Math.PI * f * t) * 0.65
                          + Math.Sin(2 * Math.PI * 2 * f * t) * 0.18
                          + Math.Sin(2 * Math.PI * 2.756 * f * t) * 0.10
                          + Math.Sin(2 * Math.PI * 3 * f * t) * 0.05;
                total += n1 * attack * decay * 0.42;
            }

            // Note 2: B5 (987.77 Hz) - Crystal bell harmonic response
            if (t >= 0.065)
            {
                double t2 = t - 0.065;
                double attack2 = t2 < 0.004 ? (t2 / 0.004) : 1.0;
                double decay2 = Math.Exp(-11.0 * t2);
                double f = 987.77;
                double n2 = Math.Sin(2 * Math.PI * f * t2) * 0.60
                          + Math.Sin(2 * Math.PI * 1.5 * f * t2) * 0.20
                          + Math.Sin(2 * Math.PI * 2 * f * t2) * 0.14
                          + Math.Sin(2 * Math.PI * 2.756 * f * t2) * 0.06;
                total += n2 * attack2 * decay2 * 0.50;
            }

            double clamped = Math.Clamp(total, -0.9, 0.9);
            samples[i] = (short)(clamped * short.MaxValue * 0.65);
        }

        return CreateWav(samples, sampleRate);
    }

    /// <summary>
    /// 💧 Waterdrop & Pop:
    /// Ultra-sleek, bouncy liquid waterdrop pop.
    /// </summary>
    private static byte[] GenerateWaterdrop()
    {
        int sampleRate = 44100;
        double duration = 0.22;
        int numSamples = (int)(sampleRate * duration);
        short[] samples = new short[numSamples];

        for (int i = 0; i < numSamples; i++)
        {
            double t = (double)i / sampleRate;
            double attack = t < 0.003 ? (t / 0.003) : 1.0;
            double decay = Math.Exp(-20.0 * t);

            // Bouncy liquid pitch swoop from 450Hz to 880Hz
            double freq = 450.0 + (880.0 - 450.0) * Math.Pow(Math.Min(1.0, t / 0.035), 0.7);
            double phase = 2 * Math.PI * freq * t;

            double body = Math.Sin(phase) * 0.75 + Math.Sin(2 * phase) * 0.22 + Math.Sin(3 * phase) * 0.06;
            double val = body * attack * decay * 0.45;
            double clamped = Math.Clamp(val, -0.9, 0.9);
            samples[i] = (short)(clamped * short.MaxValue * 0.65);
        }

        return CreateWav(samples, sampleRate);
    }

    /// <summary>
    /// 🔔 Crystal Bell Trio:
    /// Serene major triad chord (G5 -> C6 -> E6) with crystal shimmer.
    /// </summary>
    private static byte[] GenerateCrystalBell()
    {
        int sampleRate = 44100;
        double duration = 0.45;
        int numSamples = (int)(sampleRate * duration);
        short[] samples = new short[numSamples];

        for (int i = 0; i < numSamples; i++)
        {
            double t = (double)i / sampleRate;
            double total = 0.0;

            // Note 1: G5 (783.99 Hz)
            if (t < 0.32)
            {
                double attack = t < 0.006 ? (t / 0.006) : 1.0;
                double decay = Math.Exp(-11.0 * t);
                double f = 783.99;
                double n = Math.Sin(2 * Math.PI * f * t) * 0.7 + Math.Sin(2 * Math.PI * 2 * f * t) * 0.2 + Math.Sin(2 * Math.PI * 2.756 * f * t) * 0.08;
                total += n * attack * decay * 0.30;
            }

            // Note 2: C6 (1046.5 Hz)
            if (t >= 0.065 && t < 0.38)
            {
                double t2 = t - 0.065;
                double attack2 = t2 < 0.005 ? (t2 / 0.005) : 1.0;
                double decay2 = Math.Exp(-10.5 * t2);
                double f = 1046.5;
                double n = Math.Sin(2 * Math.PI * f * t2) * 0.7 + Math.Sin(2 * Math.PI * 2 * f * t2) * 0.2 + Math.Sin(2 * Math.PI * 2.756 * f * t2) * 0.08;
                total += n * attack2 * decay2 * 0.35;
            }

            // Note 3: E6 (1318.5 Hz)
            if (t >= 0.13)
            {
                double t3 = t - 0.13;
                double attack3 = t3 < 0.005 ? (t3 / 0.005) : 1.0;
                double decay3 = Math.Exp(-10.0 * t3);
                double f = 1318.5;
                double n = Math.Sin(2 * Math.PI * f * t3) * 0.7 + Math.Sin(2 * Math.PI * 2 * f * t3) * 0.2 + Math.Sin(2 * Math.PI * 2.756 * f * t3) * 0.08;
                total += n * attack3 * decay3 * 0.42;
            }

            double clamped = Math.Clamp(total, -0.9, 0.9);
            samples[i] = (short)(clamped * short.MaxValue * 0.60);
        }

        return CreateWav(samples, sampleRate);
    }

    /// <summary>
    /// Subtle Tactile Tick:
    /// Ultra-short, quiet haptic tick for island expand & interactions (not a loud beep).
    /// </summary>
    private static byte[] GenerateTactileTick()
    {
        int sampleRate = 44100;
        double duration = 0.022;
        int numSamples = (int)(sampleRate * duration);
        short[] samples = new short[numSamples];

        for (int i = 0; i < numSamples; i++)
        {
            double t = (double)i / sampleRate;
            double attack = t < 0.002 ? (t / 0.002) : 1.0;
            double decay = Math.Exp(-75.0 * t);
            double freq = 260.0 - 100.0 * (t / duration);
            double val = Math.Sin(2 * Math.PI * freq * t) * attack * decay * 0.12;
            samples[i] = (short)(val * short.MaxValue);
        }

        return CreateWav(samples, sampleRate);
    }

    private static byte[] CreateWav(short[] samples, int sampleRate)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        int subChunk2Size = samples.Length * sizeof(short);
        int chunkSize = 36 + subChunk2Size;

        writer.Write(new[] { 'R', 'I', 'F', 'F' });
        writer.Write(chunkSize);
        writer.Write(new[] { 'W', 'A', 'V', 'E' });

        writer.Write(new[] { 'f', 'm', 't', ' ' });
        writer.Write(16); // SubChunk1Size (PCM)
        writer.Write((short)1); // AudioFormat (PCM)
        writer.Write((short)1); // NumChannels (Mono)
        writer.Write(sampleRate);
        writer.Write(sampleRate * sizeof(short)); // ByteRate
        writer.Write((short)sizeof(short)); // BlockAlign
        writer.Write((short)16); // BitsPerSample

        writer.Write(new[] { 'd', 'a', 't', 'a' });
        writer.Write(subChunk2Size);
        foreach (short s in samples)
        {
            writer.Write(s);
        }

        writer.Flush();
        return ms.ToArray();
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, SoundPlayer?> _coucouPlayers = new();

    public static void PlayCoucou(string name)
    {
        if (!IsEnabled || CurrentTheme == NotificationSoundTheme.Mute) return;
        try
        {
            var player = _coucouPlayers.GetOrAdd(name, n =>
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string path = Path.Combine(baseDir, "Sounds", $"{n}.wav");
                if (!File.Exists(path))
                {
                    path = Path.Combine(Directory.GetCurrentDirectory(), "Sounds", $"{n}.wav");
                }
                if (File.Exists(path))
                {
                    var sp = new SoundPlayer(path);
                    sp.Load();
                    return sp;
                }
                return null;
            });

            player?.Play();
        }
        catch { }
    }
}
