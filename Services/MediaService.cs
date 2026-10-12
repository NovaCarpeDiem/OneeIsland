using System.IO;
using System.Windows.Media.Imaging;
using Windows.Media.Control;

namespace DynamicIslandWin.Services;

public sealed class MediaTrackInfo
{
    public string Title { get; init; } = "";
    public string Artist { get; init; } = "";
    public string SourceApp { get; init; } = "";
    public bool IsPlaying { get; init; }
    public bool HasActiveSession { get; init; }
    public BitmapImage? Thumbnail { get; init; }
    public TimeSpan Position { get; init; }
    public TimeSpan Duration { get; init; }
}

public sealed class MediaService
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private string _lastTrackSignature = "";
    private BitmapImage? _cachedThumbnail;

    private TimeSpan? _overridePosition;
    private DateTimeOffset _overrideTimestamp = DateTimeOffset.MinValue;
    private TimeSpan _lastKnownPosition = TimeSpan.Zero;
    private TimeSpan _lastKnownDuration = TimeSpan.Zero;

    public async Task InitializeAsync()
    {
        try
        {
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        }
        catch
        {
            _manager = null;
        }
    }

    public async Task<MediaTrackInfo> GetCurrentTrackAsync()
    {
        try
        {
            if (_manager == null)
            {
                await InitializeAsync();
                if (_manager == null) return new MediaTrackInfo();
            }

            var session = _manager.GetCurrentSession();
            if (session == null)
            {
                return new MediaTrackInfo { HasActiveSession = false };
            }

            var playbackInfo = session.GetPlaybackInfo();
            bool isPlaying = playbackInfo?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

            var props = await session.TryGetMediaPropertiesAsync();
            if (props == null || string.IsNullOrWhiteSpace(props.Title))
            {
                return new MediaTrackInfo
                {
                    HasActiveSession = true,
                    IsPlaying = isPlaying,
                    Title = "Medya Bekleniyor",
                    Artist = FormatAppName(session.SourceAppUserModelId)
                };
            }

            string title = props.Title.Trim();
            string artist = string.IsNullOrWhiteSpace(props.Artist)
                ? FormatAppName(session.SourceAppUserModelId)
                : props.Artist.Trim();

            string signature = $"{title}|{artist}|{session.SourceAppUserModelId}";
            if (signature != _lastTrackSignature)
            {
                _lastTrackSignature = signature;
                _overridePosition = null;
                _cachedThumbnail = await LoadThumbnailAsync(props.Thumbnail);
            }

            TimeSpan pos = TimeSpan.Zero;
            TimeSpan dur = TimeSpan.Zero;
            try
            {
                var timeline = session.GetTimelineProperties();
                if (timeline != null)
                {
                    dur = timeline.EndTime - timeline.StartTime;
                    pos = timeline.Position;

                    if (isPlaying && timeline.LastUpdatedTime.Year > 2020)
                    {
                        var elapsed = DateTimeOffset.UtcNow - timeline.LastUpdatedTime;
                        if (elapsed > TimeSpan.Zero && elapsed < TimeSpan.FromHours(3))
                        {
                            pos += elapsed;
                        }
                    }

                    if (_overridePosition.HasValue)
                    {
                        var sinceOverride = DateTimeOffset.UtcNow - _overrideTimestamp;
                        if (sinceOverride < TimeSpan.FromSeconds(2.2))
                        {
                            pos = _overridePosition.Value + (isPlaying ? sinceOverride : TimeSpan.Zero);
                        }
                        else
                        {
                            _overridePosition = null;
                        }
                    }

                    if (dur > TimeSpan.Zero && pos > dur)
                    {
                        pos = dur;
                    }
                    if (pos < TimeSpan.Zero)
                    {
                        pos = TimeSpan.Zero;
                    }
                }
            }
            catch
            {
                // Ignore timeline unsupported
            }

            _lastKnownPosition = pos;
            _lastKnownDuration = dur;

            return new MediaTrackInfo
            {
                HasActiveSession = true,
                IsPlaying = isPlaying,
                Title = title,
                Artist = artist,
                SourceApp = FormatAppName(session.SourceAppUserModelId),
                Thumbnail = _cachedThumbnail,
                Position = pos,
                Duration = dur
            };
        }
        catch
        {
            return new MediaTrackInfo();
        }
    }

    public async Task SeekAsync(TimeSpan targetPosition)
    {
        try
        {
            var session = _manager?.GetCurrentSession();
            if (session != null)
            {
                if (_lastKnownDuration > TimeSpan.Zero && targetPosition > _lastKnownDuration)
                {
                    targetPosition = _lastKnownDuration;
                }
                if (targetPosition < TimeSpan.Zero)
                {
                    targetPosition = TimeSpan.Zero;
                }

                _overridePosition = targetPosition;
                _overrideTimestamp = DateTimeOffset.UtcNow;
                await session.TryChangePlaybackPositionAsync(targetPosition.Ticks);
            }
        }
        catch
        {
        }
    }

    public async Task SkipSecondsAsync(double deltaSeconds)
    {
        var next = _lastKnownPosition + TimeSpan.FromSeconds(deltaSeconds);
        await SeekAsync(next);
    }

    public async Task PlayPauseAsync()
    {
        try
        {
            var session = _manager?.GetCurrentSession();
            if (session != null)
            {
                await session.TryTogglePlayPauseAsync();
            }
        }
        catch
        {
        }
    }

    public async Task NextAsync()
    {
        try
        {
            var session = _manager?.GetCurrentSession();
            if (session != null)
            {
                _overridePosition = TimeSpan.Zero;
                _overrideTimestamp = DateTimeOffset.UtcNow;
                await session.TrySkipNextAsync();
            }
        }
        catch
        {
        }
    }

    public async Task PreviousAsync()
    {
        try
        {
            var session = _manager?.GetCurrentSession();
            if (session != null)
            {
                _overridePosition = TimeSpan.Zero;
                _overrideTimestamp = DateTimeOffset.UtcNow;
                await session.TrySkipPreviousAsync();
            }
        }
        catch
        {
        }
    }

    private static string FormatAppName(string? appId)
    {
        if (string.IsNullOrWhiteSpace(appId)) return "Sistem Medyası";
        string lower = appId.ToLowerInvariant();
        if (lower.Contains("spotify")) return "Spotify";
        if (lower.Contains("vivaldi")) return "Vivaldi";
        if (lower.Contains("chrome")) return "Chrome";
        if (lower.Contains("msedge") || lower.Contains("edge")) return "Edge";
        if (lower.Contains("firefox")) return "Firefox";
        if (lower.Contains("vlc")) return "VLC";
        if (lower.Contains("zune") || lower.Contains("music")) return "Media Player";

        string cleaned = Path.GetFileNameWithoutExtension(appId);
        return string.IsNullOrWhiteSpace(cleaned) ? "Medya" : cleaned;
    }

    private static async Task<BitmapImage?> LoadThumbnailAsync(Windows.Storage.Streams.IRandomAccessStreamReference? thumbRef)
    {
        if (thumbRef == null) return null;
        try
        {
            using var winrtStream = await thumbRef.OpenReadAsync();
            using var netStream = winrtStream.AsStreamForRead();
            using var ms = new MemoryStream();
            await netStream.CopyToAsync(ms);
            ms.Position = 0;

            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.DecodePixelWidth = 128;
            bmp.StreamSource = ms;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch
        {
            return null;
        }
    }
}
