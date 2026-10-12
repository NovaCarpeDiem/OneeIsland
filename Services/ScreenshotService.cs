using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace DynamicIslandWin.Services;

public static class ScreenshotService
{
    private static readonly string ScreenshotsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DynamicIslandWin",
        "Screenshots");

    public static string? CaptureFullScreen()
    {
        try
        {
            Directory.CreateDirectory(ScreenshotsDir);

            var bounds = System.Windows.Forms.Screen.PrimaryScreen?.Bounds
                         ?? new System.Drawing.Rectangle(0, 0, 1920, 1080);

            using var bmp = new System.Drawing.Bitmap(bounds.Width, bounds.Height);
            using (var g = System.Drawing.Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(bounds.Location, System.Drawing.Point.Empty, bounds.Size);
            }

            string fileName = $"Ekran_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            string filePath = Path.Combine(ScreenshotsDir, fileName);
            bmp.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);

            // Copy to clipboard as well
            System.Windows.Forms.Clipboard.SetImage(bmp);

            return filePath;
        }
        catch
        {
            return null;
        }
    }

    public static void LaunchRegionSnip()
    {
        try
        {
            Process.Start(new ProcessStartInfo("ms-screenclip:") { UseShellExecute = true });
        }
        catch
        {
        }
    }

    public static string? SaveClipboardImageIfPresent()
    {
        try
        {
            if (!Clipboard.ContainsImage()) return null;

            var source = Clipboard.GetImage();
            if (source == null) return null;

            Directory.CreateDirectory(ScreenshotsDir);
            string fileName = $"Kirpma_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            string filePath = Path.Combine(ScreenshotsDir, fileName);

            using var fs = new FileStream(filePath, FileMode.Create);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            encoder.Save(fs);

            return filePath;
        }
        catch
        {
            return null;
        }
    }
}
