using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Threading;

namespace DynamicIslandWin.Services;

public class NativeAvatarFrame
{
    public int durationMs { get; set; }
    public string head { get; set; } = "";
    public string leftEye { get; set; } = "";
    public string rightEye { get; set; } = "";
    public string leftBlink { get; set; } = "";
    public string rightBlink { get; set; } = "";
    public bool leftVis { get; set; } = true;
    public bool rightVis { get; set; } = true;

    [System.Text.Json.Serialization.JsonIgnore]
    public Geometry? CachedHead { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public Geometry? CachedLeftEye { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public Geometry? CachedRightEye { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public Geometry? CachedLeftBlink { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public Geometry? CachedRightBlink { get; set; }
}

public class NativeAvatarAnimation
{
    public List<NativeAvatarFrame> frames { get; set; } = new();
}

public class NativeAvatarData
{
    public Dictionary<string, NativeAvatarAnimation> animations { get; set; } = new();
}

public class NativeAvatarRenderer
{
    private static NativeAvatarData? _cachedData;
    private static readonly object _lock = new();

    private readonly DispatcherTimer _frameTimer = new();
    private readonly DispatcherTimer _blinkTimer = new();

    private string _currentAnimation = "idle";
    private int _currentFrameIndex = 0;
    private bool _isBlinking = false;

    public event Action<Geometry, Geometry, Geometry, bool, bool>? FrameUpdated;

    public string CurrentAnimation => _currentAnimation;

    public static bool EnsureLoaded()
    {
        if (_cachedData != null) return true;
        lock (_lock)
        {
            if (_cachedData != null) return true;
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Avatar", "onee_rendered_animations.json");
                if (!File.Exists(path))
                {
                    path = Path.Combine(Directory.GetCurrentDirectory(), "Avatar", "onee_rendered_animations.json");
                }

                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    _cachedData = JsonSerializer.Deserialize<NativeAvatarData>(json);
                    if (_cachedData != null)
                    {
                        foreach (var anim in _cachedData.animations.Values)
                        {
                            foreach (var frame in anim.frames)
                            {
                                try
                                {
                                    if (!string.IsNullOrEmpty(frame.head))
                                    {
                                        var g = Geometry.Parse(frame.head);
                                        g.Freeze();
                                        frame.CachedHead = g;
                                    }
                                    if (!string.IsNullOrEmpty(frame.leftEye))
                                    {
                                        var g = Geometry.Parse(frame.leftEye);
                                        g.Freeze();
                                        frame.CachedLeftEye = g;
                                    }
                                    if (!string.IsNullOrEmpty(frame.rightEye))
                                    {
                                        var g = Geometry.Parse(frame.rightEye);
                                        g.Freeze();
                                        frame.CachedRightEye = g;
                                    }
                                    if (!string.IsNullOrEmpty(frame.leftBlink))
                                    {
                                        var g = Geometry.Parse(frame.leftBlink);
                                        g.Freeze();
                                        frame.CachedLeftBlink = g;
                                    }
                                    if (!string.IsNullOrEmpty(frame.rightBlink))
                                    {
                                        var g = Geometry.Parse(frame.rightBlink);
                                        g.Freeze();
                                        frame.CachedRightBlink = g;
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                    return _cachedData != null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NativeAvatarRenderer] Load error: {ex.Message}");
            }
            return false;
        }
    }

    public NativeAvatarRenderer()
    {
        EnsureLoaded();

        _frameTimer.Tick += FrameTimer_Tick;
        _blinkTimer.Interval = TimeSpan.FromMilliseconds(3200);
        _blinkTimer.Tick += BlinkTimer_Tick;
    }

    public void Start(string animation = "idle")
    {
        Play(animation);
        _blinkTimer.Start();
    }

    public void Stop()
    {
        _frameTimer.Stop();
        _blinkTimer.Stop();
    }

    public void Play(string animation)
    {
        if (_cachedData == null || !_cachedData.animations.ContainsKey(animation))
        {
            animation = "idle";
        }

        _currentAnimation = animation;
        _currentFrameIndex = 0;
        RenderCurrentFrame();
        ScheduleNextFrame();
    }

    private void ScheduleNextFrame()
    {
        _frameTimer.Stop();
        if (_cachedData == null || !_cachedData.animations.TryGetValue(_currentAnimation, out var anim) || anim.frames.Count == 0)
            return;

        int dur = anim.frames[_currentFrameIndex].durationMs;
        dur = Math.Clamp(dur, 40, 4000);
        _frameTimer.Interval = TimeSpan.FromMilliseconds(dur);
        _frameTimer.Start();
    }

    private void FrameTimer_Tick(object? sender, EventArgs e)
    {
        if (_cachedData == null || !_cachedData.animations.TryGetValue(_currentAnimation, out var anim) || anim.frames.Count == 0)
            return;

        _currentFrameIndex = (_currentFrameIndex + 1) % anim.frames.Count;
        RenderCurrentFrame();
        ScheduleNextFrame();
    }

    private async void BlinkTimer_Tick(object? sender, EventArgs e)
    {
        if (_cachedData == null) return;
        _isBlinking = true;
        RenderCurrentFrame();

        // 180ms blink
        await System.Threading.Tasks.Task.Delay(180);
        _isBlinking = false;
        RenderCurrentFrame();

        // Random next blink 2.5s - 5.5s
        int nextDelay = Random.Shared.Next(2500, 5500);
        _blinkTimer.Interval = TimeSpan.FromMilliseconds(nextDelay);
    }

    private static readonly Geometry _emptyGeometry = Geometry.Empty;

    private void RenderCurrentFrame()
    {
        if (_cachedData == null || !_cachedData.animations.TryGetValue(_currentAnimation, out var anim) || anim.frames.Count == 0)
            return;

        var frame = anim.frames[Math.Clamp(_currentFrameIndex, 0, anim.frames.Count - 1)];

        try
        {
            var headGeo = frame.CachedHead ?? _emptyGeometry;
            var leftGeo = (_isBlinking && frame.CachedLeftBlink != null)
                ? frame.CachedLeftBlink
                : (frame.CachedLeftEye ?? _emptyGeometry);
            var rightGeo = (_isBlinking && frame.CachedRightBlink != null)
                ? frame.CachedRightBlink
                : (frame.CachedRightEye ?? _emptyGeometry);

            FrameUpdated?.Invoke(headGeo, leftGeo, rightGeo, frame.leftVis, frame.rightVis);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NativeAvatarRenderer] Geometry render error: {ex.Message}");
        }
    }
}
