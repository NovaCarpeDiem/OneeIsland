using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DynamicIslandWin.Services;

public static class AdaptiveColorService
{
    public static readonly Color DefaultAccent = Color.FromRgb(244, 63, 94); // #F43F5E Modern Minimal Crimson
    public static readonly Color FallbackDesktopBlue = Color.FromRgb(46, 119, 229); // #2E77E5 Windows Bloom Blue

    #region Win32 Native Interop
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int SystemParametersInfo(int uAction, int uParam, StringBuilder lpvParam, int fuWinIni);

    private const int SPI_GETDESKWALLPAPER = 0x0073;

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetShellWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetDesktopWindow();

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
    #endregion

    #region Desktop Wallpaper & Active Window Color Extraction
    private static string _cachedWallpaperPath = "";
    private static DateTime _cachedWallpaperTime = DateTime.MinValue;
    private static Color _cachedWallpaperColor = FallbackDesktopBlue;
    private static readonly object _wallpaperLock = new();

    private static bool IsDesktopWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return true;
        if (hwnd == GetShellWindow() || hwnd == GetDesktopWindow()) return true;

        var sb = new StringBuilder(64);
        GetClassName(hwnd, sb, 64);
        string cls = sb.ToString();
        return cls == "Progman" || cls == "WorkerW";
    }

    private static string GetWallpaperPath()
    {
        try
        {
            var sb = new StringBuilder(500);
            SystemParametersInfo(SPI_GETDESKWALLPAPER, 500, sb, 0);
            string path = sb.ToString();
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                return path;
            }
        }
        catch { }

        try
        {
            string regWp = Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop", "Wallpaper", "")?.ToString() ?? "";
            if (!string.IsNullOrWhiteSpace(regWp) && File.Exists(regWp))
            {
                return regWp;
            }
        }
        catch { }

        try
        {
            string appDataWp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Themes\TranscodedWallpaper");
            if (File.Exists(appDataWp))
            {
                return appDataWp;
            }
        }
        catch { }

        string winWp = @"C:\Windows\web\wallpaper\Windows\img19.jpg";
        if (File.Exists(winWp))
        {
            return winWp;
        }

        return "";
    }

    public static Color GetDesktopWallpaperDominantColor()
    {
        lock (_wallpaperLock)
        {
            try
            {
                string wpPath = GetWallpaperPath();
                if (string.IsNullOrEmpty(wpPath) || !File.Exists(wpPath))
                {
                    return _cachedWallpaperColor;
                }

                var writeTime = File.GetLastWriteTimeUtc(wpPath);
                if (wpPath == _cachedWallpaperPath && writeTime == _cachedWallpaperTime)
                {
                    return _cachedWallpaperColor;
                }

                using var stream = new FileStream(wpPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var original = System.Drawing.Image.FromStream(stream);
                using var bmp = new System.Drawing.Bitmap(original, 100, 60);

                Color extracted = ExtractDominantColor(bmp, FallbackDesktopBlue);
                _cachedWallpaperPath = wpPath;
                _cachedWallpaperTime = writeTime;
                _cachedWallpaperColor = extracted;
                return extracted;
            }
            catch
            {
                return _cachedWallpaperColor;
            }
        }
    }

    public static Color GetActiveWindowOrScreenDominantColor()
    {
        try
        {
            IntPtr hwnd = GetForegroundWindow();
            if (IsDesktopWindow(hwnd))
            {
                return GetDesktopWallpaperDominantColor();
            }

            if (!GetWindowRect(hwnd, out RECT rect))
            {
                return GetDesktopWallpaperDominantColor();
            }

            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;
            if (width < 80 || height < 80)
            {
                return GetDesktopWallpaperDominantColor();
            }

            int sampleW = Math.Clamp(width, 60, 160);
            int sampleH = Math.Clamp(height, 40, 90);
            int sampleX = Math.Max(0, rect.Left + Math.Min(50, width / 4));
            int sampleY = Math.Max(0, rect.Top + Math.Min(20, height / 6));

            using var screenBmp = new System.Drawing.Bitmap(sampleW, sampleH);
            using var g = System.Drawing.Graphics.FromImage(screenBmp);
            g.CopyFromScreen(sampleX, sampleY, 0, 0, new System.Drawing.Size(sampleW, sampleH));

            Color dominant = ExtractDominantColor(screenBmp, fallback: null);
            if (dominant.A != 0)
            {
                return dominant;
            }

            // If header was monochrome (e.g. dark gray title bar), try sampling window body
            if (height > 200)
            {
                int bodyY = Math.Max(0, rect.Top + height / 3);
                g.CopyFromScreen(sampleX, bodyY, 0, 0, new System.Drawing.Size(sampleW, sampleH));
                Color bodyDominant = ExtractDominantColor(screenBmp, fallback: null);
                if (bodyDominant.A != 0)
                {
                    return bodyDominant;
                }
            }

            return GetDesktopWallpaperDominantColor();
        }
        catch
        {
            return GetDesktopWallpaperDominantColor();
        }
    }

    public static Color ExtractDominantColorAroundIsland(
        double islandLeft, double islandTop, double islandWidth, double islandHeight,
        double screenWidth, double screenHeight)
    {
        try
        {
            // Sample perimeter and backdrop around the island's current physical screen location
            int marginX = 240; // 240px to the left and right
            int marginYBottom = 180; // 180px below the island

            int sampleX = Math.Clamp((int)(islandLeft - marginX), 0, Math.Max(0, (int)screenWidth - 40));
            int sampleY = 0;
            int sampleW = Math.Clamp((int)screenWidth - sampleX, 40, (int)(islandWidth + marginX * 2));
            int sampleH = Math.Clamp((int)screenHeight, 60, Math.Max(180, (int)(islandTop + islandHeight + marginYBottom)));

            if (sampleW <= 20 || sampleH <= 20)
            {
                return GetDesktopWallpaperDominantColor();
            }

            using var screenBmp = new System.Drawing.Bitmap(sampleW, sampleH);
            using var g = System.Drawing.Graphics.FromImage(screenBmp);
            g.CopyFromScreen(sampleX, sampleY, 0, 0, new System.Drawing.Size(sampleW, sampleH));

            int localIslandLeft = (int)(islandLeft - sampleX);
            int localIslandTop = (int)(islandTop - sampleY);
            int localIslandRight = localIslandLeft + (int)islandWidth;
            int localIslandBottom = localIslandTop + (int)islandHeight;

            const int bucketCount = 18;
            double[] bucketWeights = new double[bucketCount];
            double[] sumR = new double[bucketCount];
            double[] sumG = new double[bucketCount];
            double[] sumB = new double[bucketCount];

            // Secondary bucket accumulator for low-saturation / dark wallpapers
            double[] secondaryWeights = new double[bucketCount];
            double[] secSumR = new double[bucketCount];
            double[] secSumG = new double[bucketCount];
            double[] secSumB = new double[bucketCount];

            int stepX = Math.Max(4, sampleW / 36);
            int stepY = Math.Max(4, sampleH / 24);

            for (int x = 0; x < sampleW; x += stepX)
            {
                for (int y = 0; y < sampleH; y += stepY)
                {
                    bool isDirectlyInsideIsland = (x >= localIslandLeft && x <= localIslandRight &&
                                                   y >= localIslandTop && y <= localIslandBottom);

                    System.Drawing.Color c = screenBmp.GetPixel(x, y);
                    RgbToHsl(c.R, c.G, c.B, out double h, out double sat, out double lum);

                    if (lum < 0.03 || lum > 0.96) continue;

                    int bucket = Math.Clamp((int)(h * bucketCount), 0, bucketCount - 1);
                    double edgeMultiplier = isDirectlyInsideIsland ? 0.35 : 1.8;

                    // Primary pass: standard saturation
                    if (sat >= 0.10)
                    {
                        double weight = (sat * 2.5) * (1.0 - Math.Abs(lum - 0.45)) * edgeMultiplier;
                        bucketWeights[bucket] += weight;
                        sumR[bucket] += c.R * weight;
                        sumG[bucket] += c.G * weight;
                        sumB[bucket] += c.B * weight;
                    }
                    // Secondary pass: subtle / dark wallpaper tones
                    else if (sat >= 0.03)
                    {
                        double weight = (sat * 1.5) * edgeMultiplier;
                        secondaryWeights[bucket] += weight;
                        secSumR[bucket] += c.R * weight;
                        secSumG[bucket] += c.G * weight;
                        secSumB[bucket] += c.B * weight;
                    }
                }
            }

            int bestBucket = -1;
            double bestWeight = 0;
            for (int b = 0; b < bucketCount; b++)
            {
                if (bucketWeights[b] > bestWeight)
                {
                    bestWeight = bucketWeights[b];
                    bestBucket = b;
                }
            }

            // If primary pass found no dominant colored bucket, check secondary pass
            if (bestBucket < 0 || bestWeight <= 0)
            {
                for (int b = 0; b < bucketCount; b++)
                {
                    if (secondaryWeights[b] > bestWeight)
                    {
                        bestWeight = secondaryWeights[b];
                        bestBucket = b;
                    }
                }

                if (bestBucket >= 0 && bestWeight > 0)
                {
                    byte sR = (byte)Math.Clamp(secSumR[bestBucket] / bestWeight, 0, 255);
                    byte sG = (byte)Math.Clamp(secSumG[bestBucket] / bestWeight, 0, 255);
                    byte sB = (byte)Math.Clamp(secSumB[bestBucket] / bestWeight, 0, 255);
                    RgbToHsl(sR, sG, sB, out double sH, out double sS, out double sL);
                    return HslToRgb(sH, Math.Clamp(Math.Max(sS * 2.2, 0.72), 0.72, 0.95), Math.Clamp(sL, 0.54, 0.68));
                }

                return GetDesktopWallpaperDominantColor();
            }

            byte avgR = (byte)Math.Clamp(sumR[bestBucket] / bestWeight, 0, 255);
            byte avgG = (byte)Math.Clamp(sumG[bestBucket] / bestWeight, 0, 255);
            byte avgB = (byte)Math.Clamp(sumB[bestBucket] / bestWeight, 0, 255);

            RgbToHsl(avgR, avgG, avgB, out double finalH, out double finalS, out double finalL);
            finalS = Math.Clamp(Math.Max(finalS, 0.70), 0.70, 0.96);
            finalL = Math.Clamp(finalL, 0.52, 0.65);

            return HslToRgb(finalH, finalS, finalL);
        }
        catch
        {
            return GetDesktopWallpaperDominantColor();
        }
    }
    #endregion

    #region Color Extraction Core
    public static Color ExtractDominantColor(BitmapImage? image)
    {
        if (image == null) return DefaultAccent;

        try
        {
            var formatted = new FormatConvertedBitmap();
            formatted.BeginInit();
            formatted.Source = image;
            formatted.DestinationFormat = PixelFormats.Bgra32;
            formatted.EndInit();

            int width = formatted.PixelWidth;
            int height = formatted.PixelHeight;
            if (width <= 0 || height <= 0) return DefaultAccent;

            int stride = width * 4;
            byte[] pixels = new byte[stride * height];
            formatted.CopyPixels(new System.Windows.Int32Rect(0, 0, width, height), pixels, stride, 0);

            const int bucketCount = 18;
            double[] bucketWeights = new double[bucketCount];
            double[] sumR = new double[bucketCount];
            double[] sumG = new double[bucketCount];
            double[] sumB = new double[bucketCount];

            int step = Math.Max(4, (pixels.Length / 4 / 400) * 4);
            for (int i = 0; i <= pixels.Length - 4; i += step)
            {
                byte b = pixels[i];
                byte g = pixels[i + 1];
                byte r = pixels[i + 2];

                RgbToHsl(r, g, b, out double h, out double sat, out double lum);

                if (lum < 0.05 || lum > 0.94) continue;
                if (sat < 0.14) continue;

                int bucket = Math.Clamp((int)(h * bucketCount), 0, bucketCount - 1);
                double weight = (sat * 2.2) * (1.0 - Math.Abs(lum - 0.45));

                bucketWeights[bucket] += weight;
                sumR[bucket] += r * weight;
                sumG[bucket] += g * weight;
                sumB[bucket] += b * weight;
            }

            int bestBucket = -1;
            double bestWeight = 0;
            for (int b = 0; b < bucketCount; b++)
            {
                if (bucketWeights[b] > bestWeight)
                {
                    bestWeight = bucketWeights[b];
                    bestBucket = b;
                }
            }

            if (bestBucket < 0 || bestWeight <= 0)
            {
                return DefaultAccent;
            }

            byte avgR = (byte)Math.Clamp(sumR[bestBucket] / bestWeight, 0, 255);
            byte avgG = (byte)Math.Clamp(sumG[bestBucket] / bestWeight, 0, 255);
            byte avgB = (byte)Math.Clamp(sumB[bestBucket] / bestWeight, 0, 255);

            RgbToHsl(avgR, avgG, avgB, out double finalH, out double finalS, out double finalL);
            finalS = Math.Clamp(Math.Max(finalS, 0.70), 0.70, 0.96);
            finalL = Math.Clamp(finalL, 0.52, 0.65);

            return HslToRgb(finalH, finalS, finalL);
        }
        catch
        {
            return DefaultAccent;
        }
    }

    public static Color ExtractDominantColor(System.Drawing.Bitmap? bmp, Color? fallback = null)
    {
        Color defaultColor = fallback ?? DefaultAccent;
        if (bmp == null) return defaultColor;

        try
        {
            const int bucketCount = 18;
            double[] bucketWeights = new double[bucketCount];
            double[] sumR = new double[bucketCount];
            double[] sumG = new double[bucketCount];
            double[] sumB = new double[bucketCount];

            int stepX = Math.Max(1, bmp.Width / 40);
            int stepY = Math.Max(1, bmp.Height / 30);

            for (int x = 0; x < bmp.Width; x += stepX)
            {
                for (int y = 0; y < bmp.Height; y += stepY)
                {
                    System.Drawing.Color c = bmp.GetPixel(x, y);
                    RgbToHsl(c.R, c.G, c.B, out double h, out double sat, out double lum);

                    if (lum < 0.05 || lum > 0.94) continue;
                    if (sat < 0.14) continue;

                    int bucket = Math.Clamp((int)(h * bucketCount), 0, bucketCount - 1);
                    double weight = (sat * 2.2) * (1.0 - Math.Abs(lum - 0.45));

                    bucketWeights[bucket] += weight;
                    sumR[bucket] += c.R * weight;
                    sumG[bucket] += c.G * weight;
                    sumB[bucket] += c.B * weight;
                }
            }

            int bestBucket = -1;
            double bestWeight = 0;
            for (int b = 0; b < bucketCount; b++)
            {
                if (bucketWeights[b] > bestWeight)
                {
                    bestWeight = bucketWeights[b];
                    bestBucket = b;
                }
            }

            if (bestBucket < 0 || bestWeight <= 0)
            {
                return fallback ?? Color.FromArgb(0, 0, 0, 0);
            }

            byte avgR = (byte)Math.Clamp(sumR[bestBucket] / bestWeight, 0, 255);
            byte avgG = (byte)Math.Clamp(sumG[bestBucket] / bestWeight, 0, 255);
            byte avgB = (byte)Math.Clamp(sumB[bestBucket] / bestWeight, 0, 255);

            RgbToHsl(avgR, avgG, avgB, out double finalH, out double finalS, out double finalL);
            finalS = Math.Clamp(Math.Max(finalS, 0.70), 0.70, 0.96);
            finalL = Math.Clamp(finalL, 0.52, 0.65);

            return HslToRgb(finalH, finalS, finalL);
        }
        catch
        {
            return defaultColor;
        }
    }

    private static Color HslToRgb(double h, double s, double l)
    {
        double r, g, b;
        if (s == 0)
        {
            r = g = b = l;
        }
        else
        {
            double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
            double p = 2 * l - q;
            r = HueToRgb(p, q, h + 1.0 / 3.0);
            g = HueToRgb(p, q, h);
            b = HueToRgb(p, q, h - 1.0 / 3.0);
        }

        return Color.FromRgb(
            (byte)Math.Clamp(Math.Round(r * 255), 0, 255),
            (byte)Math.Clamp(Math.Round(g * 255), 0, 255),
            (byte)Math.Clamp(Math.Round(b * 255), 0, 255));
    }

    private static double HueToRgb(double p, double q, double t)
    {
        if (t < 0) t += 1;
        if (t > 1) t -= 1;
        if (t < 1.0 / 6.0) return p + (q - p) * 6 * t;
        if (t < 1.0 / 2.0) return q;
        if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6;
        return p;
    }

    private static void RgbToHsl(byte r, byte g, byte b, out double h, out double sat, out double lum)
    {
        double rd = r / 255.0;
        double gd = g / 255.0;
        double bd = b / 255.0;

        double max = Math.Max(rd, Math.Max(gd, bd));
        double min = Math.Min(rd, Math.Min(gd, bd));
        double delta = max - min;

        lum = (max + min) / 2.0;

        if (delta == 0)
        {
            h = 0;
            sat = 0;
        }
        else
        {
            sat = lum <= 0.5 ? delta / (max + min) : delta / (2.0 - max - min);

            if (rd == max)
                h = ((gd - bd) / delta) + (gd < bd ? 6 : 0);
            else if (gd == max)
                h = ((bd - rd) / delta) + 2;
            else
                h = ((rd - gd) / delta) + 4;

            h /= 6.0;
        }
    }
    #endregion
}
