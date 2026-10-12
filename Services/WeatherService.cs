using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using Windows.Devices.Geolocation;

namespace DynamicIslandWin.Services;

public sealed class WeatherSnapshot
{
    public string TemperatureText { get; init; } = "--°C";
    public string ConditionText { get; init; } = "Hava Durumu";
    public string Emoji { get; init; } = "🌤️";
    public string City { get; init; } = "";
    public bool IsLoaded { get; init; }

    public string CompactSummary => IsLoaded ? $"{Emoji} {TemperatureText}" : "";
    public string FullSummary => IsLoaded
        ? (string.IsNullOrWhiteSpace(City) ? $"{Emoji} {TemperatureText} {ConditionText}" : $"{Emoji} {City} {TemperatureText} • {ConditionText}")
        : "🌤️ Hava durumu güncelleniyor...";
}

public sealed class WeatherService
{
    private static readonly HttpClient Http = CreateHttpClient();
    private WeatherSnapshot _cached = new();
    private DateTime _lastFetch = DateTime.MinValue;
    private bool _isFetching;

    private string? _customCity;
    private double? _customLat;
    private double? _customLon;

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("DynamicIslandWin/2.0 (Windows)");
        return client;
    }

    public void ConfigureCustomLocation(string? city, double? lat, double? lon)
    {
        _customCity = city;
        _customLat = lat;
        _customLon = lon;
        _lastFetch = DateTime.MinValue;
    }

    public void ForceRefresh()
    {
        _lastFetch = DateTime.MinValue;
    }

    public async Task<(bool Success, string CityName, double Lat, double Lon)> SearchCityCoordinatesAsync(string cityQuery)
    {
        if (string.IsNullOrWhiteSpace(cityQuery))
            return (false, "", 0, 0);

        try
        {
            string encoded = Uri.EscapeDataString(cityQuery.Trim());
            string url = $"https://geocoding-api.open-meteo.com/v1/search?name={encoded}&count=1&language=tr&format=json";
            string json = await Http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("results", out var results) &&
                results.ValueKind == JsonValueKind.Array &&
                results.GetArrayLength() > 0)
            {
                var first = results[0];
                string name = first.TryGetProperty("name", out var n) ? (n.GetString() ?? cityQuery.Trim()) : cityQuery.Trim();
                double lat = first.GetProperty("latitude").GetDouble();
                double lon = first.GetProperty("longitude").GetDouble();
                return (true, name, lat, lon);
            }
        }
        catch
        {
        }

        return (false, "", 0, 0);
    }

    public async Task<WeatherSnapshot> GetWeatherAsync()
    {
        if (_isFetching || (DateTime.UtcNow - _lastFetch).TotalMinutes < 15 && _cached.IsLoaded)
        {
            return _cached;
        }

        _isFetching = true;
        try
        {
            double lat = 41.1847;
            double lon = 41.8258;
            string city = "";
            bool hasCoordinates = false;

            // 1. Check if user set a manual custom city/district
            if (_customLat.HasValue && _customLon.HasValue && !string.IsNullOrWhiteSpace(_customCity))
            {
                lat = _customLat.Value;
                lon = _customLon.Value;
                city = _customCity!;
                hasCoordinates = true;
            }

            // 2. Primary Auto-Detection: Windows Native Geolocation (Wi-Fi Triangulation / GPS)
            if (!hasCoordinates)
            {
                try
                {
                    var access = await Geolocator.RequestAccessAsync();
                    if (access == GeolocationAccessStatus.Allowed)
                    {
                        var geolocator = new Geolocator
                        {
                            DesiredAccuracy = PositionAccuracy.Default
                        };
                        var pos = await geolocator.GetGeopositionAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(4));
                        double gLat = pos.Coordinate.Point.Position.Latitude;
                        double gLon = pos.Coordinate.Point.Position.Longitude;
                        if (!double.IsNaN(gLat) && !double.IsNaN(gLon) && (Math.Abs(gLat) > 0.01 || Math.Abs(gLon) > 0.01))
                        {
                            lat = gLat;
                            lon = gLon;
                            hasCoordinates = true;
                        }
                    }
                }
                catch
                {
                    // Fall through to reverse geocode or IP fallback
                }
            }

            // 3. If we got exact GPS/Wi-Fi coordinates from Windows Location Service, reverse-geocode the exact Turkish city name
            if (hasCoordinates && string.IsNullOrWhiteSpace(city))
            {
                city = await ReverseGeocodeTurkishCityAsync(lat, lon);
            }

            // 4. Fallback only if Windows Location Service is disabled in Windows Settings
            if (!hasCoordinates)
            {
                try
                {
                    string geoJson = await Http.GetStringAsync("http://ip-api.com/json/?fields=status,city,lat,lon&lang=tr");
                    using var geoDoc = JsonDocument.Parse(geoJson);
                    var root = geoDoc.RootElement;
                    if (root.TryGetProperty("status", out var status) && status.GetString() == "success")
                    {
                        if (root.TryGetProperty("lat", out var latProp)) lat = latProp.GetDouble();
                        if (root.TryGetProperty("lon", out var lonProp)) lon = lonProp.GetDouble();
                        if (root.TryGetProperty("city", out var cityProp)) city = cityProp.GetString() ?? "";
                        hasCoordinates = true;
                    }
                }
                catch
                {
                }
            }

            if (string.IsNullOrWhiteSpace(city))
            {
                city = "Konum";
            }

            // 5. Fetch current weather from Open-Meteo (free, no API key)
            string url = string.Format(
                CultureInfo.InvariantCulture,
                "https://api.open-meteo.com/v1/forecast?latitude={0:F4}&longitude={1:F4}&current_weather=true",
                lat, lon);

            string weatherJson = await Http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(weatherJson);
            if (doc.RootElement.TryGetProperty("current_weather", out var current))
            {
                double temp = current.GetProperty("temperature").GetDouble();
                int code = current.GetProperty("weathercode").GetInt32();
                var (emoji, desc) = MapWmoCode(code);

                _cached = new WeatherSnapshot
                {
                    TemperatureText = $"{Math.Round(temp)}°C",
                    ConditionText = desc,
                    Emoji = emoji,
                    City = city,
                    IsLoaded = true
                };
                _lastFetch = DateTime.UtcNow;
            }
        }
        catch
        {
        }
        finally
        {
            _isFetching = false;
        }

        return _cached;
    }

    private static async Task<string> ReverseGeocodeTurkishCityAsync(double lat, double lon)
    {
        // Primary Reverse Geocoder: BigDataCloud (fast, free, keyless, Turkish support)
        try
        {
            string bdcUrl = string.Format(
                CultureInfo.InvariantCulture,
                "https://api.bigdatacloud.net/data/reverse-geocode-client?latitude={0:F5}&longitude={1:F5}&localityLanguage=tr",
                lat, lon);

            string json = await Http.GetStringAsync(bdcUrl);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string province = root.TryGetProperty("principalSubdivision", out var p) ? (p.GetString() ?? "").Trim() : "";
            string city = root.TryGetProperty("city", out var c) ? (c.GetString() ?? "").Trim() : "";
            string locality = root.TryGetProperty("locality", out var l) ? (l.GetString() ?? "").Trim() : "";

            // Clean up " İli" / " Province" suffixes if present
            province = CleanCityName(province);
            city = CleanCityName(city);
            locality = CleanCityName(locality);

            if (!string.IsNullOrWhiteSpace(province))
            {
                if (!string.IsNullOrWhiteSpace(locality) &&
                    !locality.Equals(province, StringComparison.OrdinalIgnoreCase) &&
                    !locality.Contains("Merkez", StringComparison.OrdinalIgnoreCase))
                {
                    return $"{province}, {locality}";
                }
                return province;
            }

            if (!string.IsNullOrWhiteSpace(city)) return city;
            if (!string.IsNullOrWhiteSpace(locality)) return locality;
        }
        catch
        {
        }

        // Secondary Reverse Geocoder: OpenStreetMap Nominatim
        try
        {
            string nomUrl = string.Format(
                CultureInfo.InvariantCulture,
                "https://nominatim.openstreetmap.org/reverse?format=json&lat={0:F5}&lon={1:F5}&zoom=10&accept-language=tr",
                lat, lon);

            string json = await Http.GetStringAsync(nomUrl);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("address", out var addr))
            {
                if (addr.TryGetProperty("province", out var prov) && !string.IsNullOrWhiteSpace(prov.GetString()))
                    return CleanCityName(prov.GetString()!);
                if (addr.TryGetProperty("city", out var c) && !string.IsNullOrWhiteSpace(c.GetString()))
                    return CleanCityName(c.GetString()!);
                if (addr.TryGetProperty("town", out var t) && !string.IsNullOrWhiteSpace(t.GetString()))
                    return CleanCityName(t.GetString()!);
            }
        }
        catch
        {
        }

        return "";
    }

    private static string CleanCityName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        return raw
            .Replace(" İli", "", StringComparison.OrdinalIgnoreCase)
            .Replace(" Province", "", StringComparison.OrdinalIgnoreCase)
            .Replace(" Merkez", "", StringComparison.OrdinalIgnoreCase)
            .Trim();
    }

    private static (string Emoji, string Description) MapWmoCode(int code) => code switch
    {
        0 => ("☀️", "Açık"),
        1 or 2 => ("🌤️", "Az Bulutlu"),
        3 => ("☁️", "Bulutlu"),
        45 or 48 => ("🌫️", "Sisli"),
        51 or 53 or 55 => ("🌦️", "Çisenti"),
        61 or 63 or 65 => ("🌧️", "Yağmurlu"),
        71 or 73 or 75 or 77 => ("❄️", "Karlı"),
        80 or 81 or 82 => ("🌦️", "Sağanak"),
        95 or 96 or 99 => ("⛈️", "Fırtınalı"),
        _ => ("🌤️", "Normal")
    };
}
