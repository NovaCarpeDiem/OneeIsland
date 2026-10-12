using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace DynamicIslandWin.Services;

public sealed class LyricLine
{
    public TimeSpan Time { get; set; }
    public string Text { get; set; } = "";
}

public sealed class LyricsData
{
    public bool Found { get; set; }
    public bool HasSynced { get; set; }
    public bool IsSynthesized { get; set; }
    public string Title { get; set; } = "";
    public string Artist { get; set; } = "";
    public List<LyricLine> Lines { get; set; } = new();
    public string PlainLyrics { get; set; } = "";
}

public static class LyricsService
{
    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(4)
    };

    private static readonly ConcurrentDictionary<string, LyricsData> _cache = new();
    private static readonly Regex LrcLineRegex = new(@"\[(?<min>\d+):(?<sec>\d+(?:\.\d+)?)\](?<text>.*)", RegexOptions.Compiled);
    
    // Noise patterns inside parentheses or brackets
    private static readonly Regex BracketNoiseRegex = new(@"\s*[\(\[][^\)\]]*(?:feat|ft\.|official|video|audio|lyrics|remaster|remix|hd|4k|mv|akustik|acoustic|live|version|edit|klip)[^\)\]]*[\)\]]", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    
    // Trailing dash tags: e.g. " - Remastered 2021", " - Live at ...", " - Akustik"
    private static readonly Regex TrailingDashTagRegex = new(@"\s+-\s+(?:remaster|remastered|live|akustik|acoustic|radio edit|bonus track|instrumental|deluxe|mix|mono|stereo).*", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    static LyricsService()
    {
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "DynamicIslandWin v2.0 (https://github.com/dynamic-island-win)");
    }

    public static async Task<LyricsData?> GetLyricsAsync(string rawTitle, string rawArtist, TimeSpan? duration = null)
    {
        if (string.IsNullOrWhiteSpace(rawTitle)) return null;

        var (cleanTitle, cleanArtist) = CleanTitleAndArtist(rawTitle, rawArtist);
        if (string.IsNullOrWhiteSpace(cleanTitle)) return null;

        string cacheKey = $"{cleanTitle.ToLowerInvariant()}|{cleanArtist.ToLowerInvariant()}";
        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        string asciiTitle = ToAscii(cleanTitle);
        string asciiArtist = ToAscii(cleanArtist);

        try
        {
            // 1. Try exact match /api/get (standard title & artist)
            var exactData = await TryGetExactAsync(cleanTitle, cleanArtist, duration);
            if (exactData != null && exactData.Found)
            {
                _cache[cacheKey] = exactData;
                return exactData;
            }

            // 2. Try exact match /api/get with ASCII-folded title & artist (circumvents WAF / unicode mismatch)
            if (asciiTitle != cleanTitle || asciiArtist != cleanArtist)
            {
                var asciiExactData = await TryGetExactAsync(asciiTitle, asciiArtist, duration);
                if (asciiExactData != null && asciiExactData.Found)
                {
                    asciiExactData.Title = cleanTitle;
                    asciiExactData.Artist = cleanArtist;
                    _cache[cacheKey] = asciiExactData;
                    return asciiExactData;
                }
            }

            // 3. Search query cascade: clean combo, ascii combo, then title only
            var searchQueries = new List<string>
            {
                $"{cleanTitle} {cleanArtist}".Trim(),
                $"{asciiTitle} {asciiArtist}".Trim(),
                cleanTitle,
                asciiTitle
            };

            var tried = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var query in searchQueries)
            {
                if (string.IsNullOrWhiteSpace(query) || !tried.Add(query)) continue;

                var searchData = await TrySearchAsync(query, cleanTitle, cleanArtist, duration);
                if (searchData != null && searchData.Found)
                {
                    _cache[cacheKey] = searchData;
                    return searchData;
                }
            }
        }
        catch
        {
        }

        var notFound = new LyricsData { Found = false, Title = cleanTitle, Artist = cleanArtist };
        _cache[cacheKey] = notFound;
        return notFound;
    }

    private static async Task<LyricsData?> TryGetExactAsync(string title, string artist, TimeSpan? duration)
    {
        try
        {
            string url = $"https://lrclib.net/api/get?track_name={Uri.EscapeDataString(title)}&artist_name={Uri.EscapeDataString(artist)}";
            if (duration.HasValue && duration.Value.TotalSeconds > 10)
            {
                url += $"&duration={(int)duration.Value.TotalSeconds}";
            }

            using var resp = await _httpClient.GetAsync(url);
            if (resp.IsSuccessStatusCode)
            {
                string json = await resp.Content.ReadAsStringAsync();
                var data = ParseLrcDto(json, title, artist, duration);
                if (data != null && data.Found) return data;
            }
        }
        catch
        {
        }
        return null;
    }

    private static async Task<LyricsData?> TrySearchAsync(string query, string fallbackTitle, string fallbackArtist, TimeSpan? duration)
    {
        try
        {
            string searchUrl = $"https://lrclib.net/api/search?q={Uri.EscapeDataString(query)}";
            using var searchResp = await _httpClient.GetAsync(searchUrl);
            if (searchResp.IsSuccessStatusCode)
            {
                string searchJson = await searchResp.Content.ReadAsStringAsync();
                var list = JsonSerializer.Deserialize<List<LrcItemDto>>(searchJson, JsonOptions);
                if (list != null && list.Count > 0)
                {
                    // Preference order:
                    // 1. Synced lyrics matching artist
                    // 2. Any synced lyrics
                    // 3. Plain lyrics matching artist
                    // 4. First item
                    var match = list.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.SyncedLyrics) && MatchesArtist(x.ArtistName, fallbackArtist))
                             ?? list.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.SyncedLyrics))
                             ?? list.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.PlainLyrics) && MatchesArtist(x.ArtistName, fallbackArtist))
                             ?? list[0];

                    var data = ConvertDtoToData(match, fallbackTitle, fallbackArtist, duration);
                    if (data.Found) return data;
                }
            }
        }
        catch
        {
        }
        return null;
    }

    private static bool MatchesArtist(string? candidate, string? target)
    {
        if (string.IsNullOrWhiteSpace(candidate) || string.IsNullOrWhiteSpace(target)) return false;
        string c = ToAscii(candidate).ToLowerInvariant();
        string t = ToAscii(target).ToLowerInvariant();
        return c.Contains(t) || t.Contains(c);
    }

    public static int GetCurrentLineIndex(LyricsData data, TimeSpan currentPosition)
    {
        if (!data.HasSynced || data.Lines.Count == 0) return -1;

        int activeIdx = -1;
        for (int i = 0; i < data.Lines.Count; i++)
        {
            if (data.Lines[i].Time <= currentPosition)
            {
                activeIdx = i;
            }
            else
            {
                break;
            }
        }
        return activeIdx;
    }

    public static string ToAscii(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        return text
            .Replace("ı", "i")
            .Replace("İ", "I")
            .Replace("ğ", "g")
            .Replace("Ğ", "G")
            .Replace("ü", "u")
            .Replace("Ü", "U")
            .Replace("ş", "s")
            .Replace("Ş", "S")
            .Replace("ö", "o")
            .Replace("Ö", "O")
            .Replace("ç", "c")
            .Replace("Ç", "C")
            .Replace("â", "a")
            .Replace("î", "i")
            .Replace("û", "u")
            .Replace("é", "e");
    }

    public static (string CleanTitle, string CleanArtist) CleanTitleAndArtist(string rawTitle, string rawArtist)
    {
        if (string.IsNullOrWhiteSpace(rawTitle)) return ("", "");

        string title = rawTitle.Trim();
        string artist = (rawArtist ?? "").Trim();

        // 1. If artist is empty and title has "Artist - Song", split them
        if (string.IsNullOrWhiteSpace(artist))
        {
            int dash = title.IndexOf(" - ", StringComparison.Ordinal);
            if (dash > 0)
            {
                artist = title.Substring(0, dash).Trim();
                title = title.Substring(dash + 3).Trim();
            }
        }
        else
        {
            // If title begins with "Artist - ", remove the artist prefix
            if (title.StartsWith(artist + " - ", StringComparison.OrdinalIgnoreCase))
            {
                title = title.Substring(artist.Length + 3).Trim();
            }
        }

        // 2. Remove bracketed noise: (feat. X), (Official Video), [Lyrics], etc.
        title = BracketNoiseRegex.Replace(title, "").Trim();

        // 3. Remove trailing dash tags: " - Remastered", " - Live", " - Akustik", etc.
        title = TrailingDashTagRegex.Replace(title, "").Trim();

        // 4. Remove leftover quotes and edge punctuation
        title = title.Trim('"', '\'', '“', '”', '‘', '’', '-', ' ');

        // 5. Clean artist
        if (!string.IsNullOrWhiteSpace(artist))
        {
            int sep = artist.IndexOfAny(new[] { ',', '&', ';', '/' });
            if (sep > 0)
            {
                artist = artist.Substring(0, sep).Trim();
            }
            artist = artist.Trim('"', '\'', '“', '”', '‘', '’', '-', ' ');
        }

        return (title, artist);
    }

    private static LyricsData? ParseLrcDto(string json, string title, string artist, TimeSpan? duration)
    {
        try
        {
            var dto = JsonSerializer.Deserialize<LrcItemDto>(json, JsonOptions);
            if (dto == null) return null;
            return ConvertDtoToData(dto, title, artist, duration);
        }
        catch
        {
            return null;
        }
    }

    private static LyricsData ConvertDtoToData(LrcItemDto dto, string fallbackTitle, string fallbackArtist, TimeSpan? duration)
    {
        var result = new LyricsData
        {
            Title = string.IsNullOrWhiteSpace(dto.TrackName) ? fallbackTitle : dto.TrackName,
            Artist = string.IsNullOrWhiteSpace(dto.ArtistName) ? fallbackArtist : dto.ArtistName,
            PlainLyrics = dto.PlainLyrics ?? ""
        };

        // 1. Prefer true synchronized LRC lyrics
        if (!string.IsNullOrWhiteSpace(dto.SyncedLyrics))
        {
            var lines = ParseSyncedLrc(dto.SyncedLyrics);
            if (lines.Count > 0)
            {
                result.Lines = lines;
                result.HasSynced = true;
                result.Found = true;
                result.IsSynthesized = false;
                return result;
            }
        }

        // 2. Fallback to PlainLyrics with intelligent synthesized timeline progression!
        if (!string.IsNullOrWhiteSpace(dto.PlainLyrics))
        {
            result.Found = true;
            var synthLines = SynthesizePlainLyricsTiming(dto.PlainLyrics, duration ?? (dto.Duration > 10 ? TimeSpan.FromSeconds(dto.Duration) : null));
            if (synthLines.Count > 0)
            {
                result.Lines = synthLines;
                result.HasSynced = true;
                result.IsSynthesized = true;
            }
        }

        return result;
    }

    /// <summary>
    /// Intelligently distributes plain unsynced lyric lines across the song duration
    /// so the Dynamic Island can display the real lyrics progressing live instead of a static message!
    /// </summary>
    private static List<LyricLine> SynthesizePlainLyricsTiming(string plainLyrics, TimeSpan? duration)
    {
        var rawLines = plainLyrics
            .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x) && !x.StartsWith("[") && !x.StartsWith("Verse", StringComparison.OrdinalIgnoreCase) && !x.StartsWith("Chorus", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (rawLines.Count == 0) return new List<LyricLine>();

        double totalSec = duration.HasValue && duration.Value.TotalSeconds > 30 ? duration.Value.TotalSeconds : 180.0;
        
        // Allow intro instrumental buffer (~6-10s)
        double introSec = Math.Min(10.0, totalSec * 0.06);
        double outroSec = Math.Min(12.0, totalSec * 0.08);
        double usableSec = Math.Max(20.0, totalSec - introSec - outroSec);

        double secPerLine = Math.Clamp(usableSec / rawLines.Count, 2.8, 7.5);

        var list = new List<LyricLine>(rawLines.Count);
        double currentSec = introSec;

        for (int i = 0; i < rawLines.Count; i++)
        {
            list.Add(new LyricLine
            {
                Time = TimeSpan.FromSeconds(currentSec),
                Text = rawLines[i]
            });
            currentSec += secPerLine;
        }

        return list;
    }

    private static List<LyricLine> ParseSyncedLrc(string lrcContent)
    {
        var list = new List<LyricLine>();
        using var reader = new StringReader(lrcContent);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            line = line.Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;

            var match = LrcLineRegex.Match(line);
            if (match.Success)
            {
                if (int.TryParse(match.Groups["min"].Value, out int min) &&
                    double.TryParse(match.Groups["sec"].Value, System.Globalization.CultureInfo.InvariantCulture, out double sec))
                {
                    string text = match.Groups["text"].Value.Trim();
                    if (!string.IsNullOrEmpty(text) && text != "''")
                    {
                        list.Add(new LyricLine
                        {
                            Time = TimeSpan.FromMinutes(min) + TimeSpan.FromSeconds(sec),
                            Text = text
                        });
                    }
                }
            }
        }

        return list.OrderBy(x => x.Time).ToList();
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class LrcItemDto
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("trackName")]
        public string? TrackName { get; set; }

        [JsonPropertyName("artistName")]
        public string? ArtistName { get; set; }

        [JsonPropertyName("albumName")]
        public string? AlbumName { get; set; }

        [JsonPropertyName("duration")]
        public double Duration { get; set; }

        [JsonPropertyName("plainLyrics")]
        public string? PlainLyrics { get; set; }

        [JsonPropertyName("syncedLyrics")]
        public string? SyncedLyrics { get; set; }
    }
}
