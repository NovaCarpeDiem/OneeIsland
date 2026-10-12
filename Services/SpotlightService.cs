using System.Diagnostics;
using System.IO;

namespace DynamicIslandWin.Services;

public enum SpotlightResultType
{
    MathResult,
    Application,
    WebGoogle,
    WebYouTube
}

public sealed class SpotlightResultItem
{
    public SpotlightResultType Type { get; set; }
    public string Title { get; set; } = "";
    public string Subtitle { get; set; } = "";
    public string Icon { get; set; } = "🔍";
    public string ActionPayload { get; set; } = "";
}

public static class SpotlightService
{
    private static readonly List<(string Name, string Path)> IndexedApps = new();
    private static bool _isIndexed = false;

    public static void InitializeIndex()
    {
        if (_isIndexed) return;
        Task.Run(() =>
        {
            try
            {
                var list = new List<(string Name, string Path)>();

                // Known system tools
                list.Add(("Hesap Makinesi", "calc.exe"));
                list.Add(("Not Defteri", "notepad.exe"));
                list.Add(("Komut İstemi (CMD)", "cmd.exe"));
                list.Add(("PowerShell", "powershell.exe"));
                list.Add(("Görev Yöneticisi", "taskmgr.exe"));
                list.Add(("Paint", "mspaint.exe"));
                list.Add(("Ayarlar", "ms-settings:"));

                // Start Menu directories
                string p1 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs");
                string p2 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs");

                ScanDirectory(p1, list);
                ScanDirectory(p2, list);

                lock (IndexedApps)
                {
                    IndexedApps.Clear();
                    IndexedApps.AddRange(list.DistinctBy(a => a.Name.ToLowerInvariant()));
                    _isIndexed = true;
                }
            }
            catch
            {
            }
        });
    }

    private static void ScanDirectory(string dir, List<(string Name, string Path)> list)
    {
        if (!Directory.Exists(dir)) return;
        try
        {
            foreach (string file in Directory.EnumerateFiles(dir, "*.lnk", SearchOption.AllDirectories))
            {
                string name = Path.GetFileNameWithoutExtension(file);
                if (name.Contains("Uninstall", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Kaldır", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Help", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Website", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                list.Add((name, file));
            }
        }
        catch
        {
        }
    }

    public static List<SpotlightResultItem> Search(string query)
    {
        var results = new List<SpotlightResultItem>();
        if (string.IsNullOrWhiteSpace(query)) return results;
        query = query.Trim();

        // 1. Math Evaluation Check
        if (SimpleMathEvaluator.TryEvaluate(query, out double mathVal))
        {
            string formatted = mathVal % 1 == 0 ? $"{mathVal:0}" : $"{mathVal:0.####}";
            results.Add(new SpotlightResultItem
            {
                Type = SpotlightResultType.MathResult,
                Title = $"= {formatted}",
                Subtitle = $"Hesaplama: {query} (Enter: Panoya Kopyala)",
                Icon = "🧮",
                ActionPayload = formatted
            });
        }

        // 2. Application Matching
        lock (IndexedApps)
        {
            var matched = IndexedApps
                .Where(a => a.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(a => a.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase))
                .Take(4)
                .ToList();

            foreach (var app in matched)
            {
                results.Add(new SpotlightResultItem
                {
                    Type = SpotlightResultType.Application,
                    Title = app.Name,
                    Subtitle = "Windows Uygulaması (Enter: Başlat)",
                    Icon = GetAppIconEmoji(app.Name),
                    ActionPayload = app.Path
                });
            }
        }

        // 3. Web Search Actions
        results.Add(new SpotlightResultItem
        {
            Type = SpotlightResultType.WebGoogle,
            Title = $"Google'da Ara: \"{query}\"",
            Subtitle = "Varsayılan web tarayıcısında ara ↗",
            Icon = "🌐",
            ActionPayload = $"https://www.google.com/search?q={Uri.EscapeDataString(query)}"
        });

        results.Add(new SpotlightResultItem
        {
            Type = SpotlightResultType.WebYouTube,
            Title = $"YouTube'da Ara: \"{query}\"",
            Subtitle = "Videoları YouTube'da aç ↗",
            Icon = "▶️",
            ActionPayload = $"https://www.youtube.com/results?search_query={Uri.EscapeDataString(query)}"
        });

        return results;
    }

    private static string GetAppIconEmoji(string name)
    {
        string lower = name.ToLowerInvariant();
        if (lower.Contains("chrome") || lower.Contains("edge") || lower.Contains("browser") || lower.Contains("firefox")) return "🌐";
        if (lower.Contains("spotify") || lower.Contains("music") || lower.Contains("müzik")) return "🎵";
        if (lower.Contains("discord") || lower.Contains("telegram") || lower.Contains("chat")) return "💬";
        if (lower.Contains("code") || lower.Contains("visual studio") || lower.Contains("terminal") || lower.Contains("cmd")) return "💻";
        if (lower.Contains("steam") || lower.Contains("game") || lower.Contains("epic") || lower.Contains("riot")) return "🎮";
        if (lower.Contains("calc") || lower.Contains("hesap")) return "🧮";
        if (lower.Contains("note") || lower.Contains("not")) return "📝";
        if (lower.Contains("paint") || lower.Contains("photoshop")) return "🎨";
        return "🚀";
    }
}

public static class SimpleMathEvaluator
{
    public static bool TryEvaluate(string expression, out double result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(expression)) return false;
        expression = expression.Replace(" ", "").Replace("x", "*").Replace("X", "*").Replace(",", ".");
        if (!expression.Any(c => c is '+' or '-' or '*' or '/' or '^' or '%')) return false;

        try
        {
            int pos = 0;
            result = ParseExpression(expression, ref pos);
            return pos == expression.Length && !double.IsNaN(result) && !double.IsInfinity(result);
        }
        catch
        {
            return false;
        }
    }

    private static double ParseExpression(string s, ref int pos)
    {
        double x = ParseTerm(s, ref pos);
        while (pos < s.Length)
        {
            char op = s[pos];
            if (op != '+' && op != '-') break;
            pos++;
            double y = ParseTerm(s, ref pos);
            x = (op == '+') ? x + y : x - y;
        }
        return x;
    }

    private static double ParseTerm(string s, ref int pos)
    {
        double x = ParseFactor(s, ref pos);
        while (pos < s.Length)
        {
            char op = s[pos];
            if (op != '*' && op != '/' && op != '%') break;
            pos++;
            double y = ParseFactor(s, ref pos);
            if (op == '*') x *= y;
            else if (op == '/') x = y != 0 ? x / y : double.NaN;
            else if (op == '%') x = y != 0 ? x % y : double.NaN;
        }
        return x;
    }

    private static double ParseFactor(string s, ref int pos)
    {
        if (pos < s.Length && s[pos] == '+') { pos++; return ParseFactor(s, ref pos); }
        if (pos < s.Length && s[pos] == '-') { pos++; return -ParseFactor(s, ref pos); }

        double x = 0;
        if (pos < s.Length && s[pos] == '(')
        {
            pos++;
            x = ParseExpression(s, ref pos);
            if (pos < s.Length && s[pos] == ')') pos++;
        }
        else
        {
            int start = pos;
            while (pos < s.Length && (char.IsDigit(s[pos]) || s[pos] == '.')) pos++;
            if (pos > start)
            {
                double.TryParse(s.Substring(start, pos - start), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out x);
            }
        }

        if (pos < s.Length && s[pos] == '^')
        {
            pos++;
            double p = ParseFactor(s, ref pos);
            x = Math.Pow(x, p);
        }
        return x;
    }
}
