using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Windows.Devices.Enumeration;

namespace DynamicIslandWin.Services;

public sealed class SystemStatsSnapshot
{
    public int CpuPercent { get; init; }
    public string CpuModelName { get; init; } = "CPU İşlemci";
    public string CpuFrequencyText { get; init; } = "3.6 GHz";
    public string CpuTempText { get; init; } = "45°C";
    public int[] CpuHistory { get; init; } = { 18, 24, 20, 35, 28 };
    public int GpuPercent { get; init; } = 10;
    public string GpuModelName { get; init; } = "RTX 3050";
    public string GpuTempText { get; init; } = "50°C";
    public string GpuMemoryText { get; init; } = "1.2 / 6 GB";
    public int[] GpuHistory { get; init; } = { 10, 14, 12, 18, 15 };
    public int RamPercent { get; init; }
    public double RamUsedGb { get; init; }
    public double RamTotalGb { get; init; }
    public int[] RamHistory { get; init; } = { 52, 55, 58, 60, 62 };
    public int BatteryPercent { get; init; }
    public bool HasBattery { get; init; }
    public bool IsCharging { get; init; }
    public string BluetoothSummary { get; init; } = "Cihaz yok";
    public bool HasBluetoothDevice { get; init; }
    public string NetworkSummary { get; init; } = "Wi-Fi";
    public string NetworkSpeedSummary { get; init; } = "↓ 0 KB/s  ↑ 0 KB/s";
    public string CompactDownloadText { get; init; } = "";
    public double DownloadMbps { get; init; }
}

public sealed class SystemStatsService
{
    [StructLayout(LayoutKind.Sequential)]
    private struct FILETIME
    {
        public uint dwLowDateTime;
        public uint dwHighDateTime;

        public ulong ToUInt64() => ((ulong)dwHighDateTime << 32) | dwLowDateTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(out FILETIME lpIdleTime, out FILETIME lpKernelTime, out FILETIME lpUserTime);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private class MEMORYSTATUSEX
    {
        public uint dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("shell32.dll")]
    private static extern int SHQueryUserNotificationState(out int pquns);

    private ulong _prevIdleTime;
    private ulong _prevKernelTime;
    private ulong _prevUserTime;
    private int _lastCpuPercent;
    private string _cachedBtSummary = "Cihaz yok";
    private bool _hasBtDevice;
    private DateTime _lastBtCheck = DateTime.MinValue;

    private readonly int[] _cpuHistory = { 16, 22, 19, 28, 20 };
    private readonly int[] _gpuHistory = { 10, 14, 12, 18, 15 };
    private readonly int[] _ramHistory = { 54, 56, 58, 60, 62 };
    private static readonly string CachedCpuModel = ReadProcessorModelName();
    private static readonly double CachedBaseGhz = ReadProcessorBaseGhz();
    private string _cachedGpuModel = ReadFallbackGpuModel();
    private int _lastGpuPercent = 10;
    private string _lastGpuTemp = "50°C";
    private string _lastGpuMemory = "1.2 / 6 GB";
    private DateTime _lastGpuCheckTime = DateTime.MinValue;
    private bool _hasNvidiaSmi = true;

    private long _prevBytesReceived = -1;
    private long _prevBytesSent = -1;
    private DateTime _prevNetSampleTime = DateTime.MinValue;
    private string _lastSpeedSummary = "↓ 0 KB/s  •  ↑ 0 KB/s";
    private string _lastCompactDown = "";
    private double _lastDownMBps;

    private static string ReadProcessorModelName()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            string raw = key?.GetValue("ProcessorNameString")?.ToString()?.Trim() ?? "";
            if (!string.IsNullOrWhiteSpace(raw))
            {
                raw = raw.Replace("(R)", "").Replace("(TM)", "").Replace("Processor", "").Replace("CPU", "").Trim();
                int atIdx = raw.IndexOf('@');
                if (atIdx > 0) raw = raw[..atIdx].Trim();
                // Keep concise (e.g., "AMD Ryzen 5 5600X" or "Intel Core i7")
                var parts = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                return string.Join(" ", parts.Take(4));
            }
        }
        catch
        {
        }
        return $"{Environment.ProcessorCount} Çekirdek CPU";
    }

    private static double ReadProcessorBaseGhz()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            if (key?.GetValue("~MHz") is int mhz && mhz > 500)
            {
                return Math.Round(mhz / 1000.0, 2);
            }
        }
        catch
        {
        }
        return 3.40;
    }

    public static bool IsFullscreenAppActive(IntPtr islandHwnd)
    {
        try
        {
            // Check Direct3D exclusive fullscreen state
            if (SHQueryUserNotificationState(out int quns) == 0 && quns == 3) // QUNS_RUNNING_D3D_FULL_SCREEN
            {
                return true;
            }

            IntPtr fg = GetForegroundWindow();
            if (fg == IntPtr.Zero || fg == islandHwnd) return false;

            var sb = new StringBuilder(64);
            GetClassName(fg, sb, sb.Capacity);
            string cls = sb.ToString();

            // Ignore Windows Desktop, Taskbar, Lockscreen & System overlays
            if (string.IsNullOrEmpty(cls) || cls is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or "Windows.UI.Core.CoreWindow")
            {
                return false;
            }

            if (!GetWindowRect(fg, out RECT r)) return false;

            // Must have valid positive dimensions
            int width = r.Right - r.Left;
            int height = r.Bottom - r.Top;
            if (width < 200 || height < 200) return false;

            var screen = Screen.PrimaryScreen?.Bounds ?? new System.Drawing.Rectangle(0, 0, 1920, 1080);
            bool coversFullMonitor =
                r.Left <= screen.Left &&
                r.Top <= screen.Top &&
                r.Right >= screen.Right &&
                r.Bottom >= screen.Bottom;

            return coversFullMonitor;
        }
        catch
        {
            return false;
        }
    }

    public (string FullSummary, string CompactDown, double DownMBps) SampleNetworkSpeed()
    {
        try
        {
            var now = DateTime.UtcNow;
            double elapsedSec = (now - _prevNetSampleTime).TotalSeconds;
            if (elapsedSec < 0.4 && _prevBytesReceived >= 0)
            {
                return (_lastSpeedSummary, _lastCompactDown, _lastDownMBps);
            }

            long totalRx = 0;
            long totalTx = 0;

            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up ||
                    nic.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                    nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                {
                    continue;
                }

                var stats = nic.GetIPStatistics();
                totalRx += stats.BytesReceived;
                totalTx += stats.BytesSent;
            }

            if (_prevBytesReceived >= 0 && elapsedSec > 0)
            {
                double rxPerSec = Math.Max(0, (totalRx - _prevBytesReceived) / elapsedSec);
                double txPerSec = Math.Max(0, (totalTx - _prevBytesSent) / elapsedSec);

                _lastDownMBps = rxPerSec / (1024.0 * 1024.0);
                string downStr = FormatSpeed(rxPerSec);
                string upStr = FormatSpeed(txPerSec);

                _lastCompactDown = _lastDownMBps >= 0.25 ? $"↓ {downStr}" : "";
                _lastSpeedSummary = $"↓ {downStr}  •  ↑ {upStr}";
            }

            _prevBytesReceived = totalRx;
            _prevBytesSent = totalTx;
            _prevNetSampleTime = now;
        }
        catch
        {
        }

        return (_lastSpeedSummary, _lastCompactDown, _lastDownMBps);
    }

    private static string FormatSpeed(double bytesPerSec)
    {
        double kb = bytesPerSec / 1024.0;
        if (kb < 1000)
        {
            return $"{kb:0} KB/s";
        }
        double mb = kb / 1024.0;
        return $"{mb:0.0} MB/s";
    }

    public async Task<SystemStatsSnapshot> GetSnapshotAsync()
    {
        int cpu = ReadCpuUsage();
        var (ramPct, usedGb, totalGb) = ReadRamUsage();
        var (batPct, hasBat, isCharging) = ReadBatteryStatus();

        await QueryGpuStatusAsync();

        // Shift rolling 5-bar histories
        Array.Copy(_cpuHistory, 1, _cpuHistory, 0, 4);
        _cpuHistory[4] = Math.Clamp(cpu, 8, 100);

        Array.Copy(_gpuHistory, 1, _gpuHistory, 0, 4);
        _gpuHistory[4] = Math.Clamp(_lastGpuPercent, 5, 100);

        Array.Copy(_ramHistory, 1, _ramHistory, 0, 4);
        _ramHistory[4] = Math.Clamp(ramPct, 12, 100);

        double dynamicGhz = Math.Round(CachedBaseGhz + (cpu / 100.0) * 0.75, 1);
        int estTemp = Math.Clamp(39 + (int)Math.Round(cpu * 0.36), 38, 84);

        if ((DateTime.UtcNow - _lastBtCheck).TotalSeconds >= 8)
        {
            _lastBtCheck = DateTime.UtcNow;
            await RefreshBluetoothStatusAsync();
        }

        string netSummary = ReadNetworkStatus();
        var (speedSummary, compactDown, downMBps) = SampleNetworkSpeed();

        return new SystemStatsSnapshot
        {
            CpuPercent = cpu,
            CpuModelName = CachedCpuModel,
            CpuFrequencyText = $"{dynamicGhz:0.0} GHz",
            CpuTempText = $"{estTemp}°C",
            CpuHistory = (int[])_cpuHistory.Clone(),
            GpuPercent = _lastGpuPercent,
            GpuModelName = _cachedGpuModel,
            GpuTempText = _lastGpuTemp,
            GpuMemoryText = _lastGpuMemory,
            GpuHistory = (int[])_gpuHistory.Clone(),
            RamPercent = ramPct,
            RamUsedGb = usedGb,
            RamTotalGb = totalGb,
            RamHistory = (int[])_ramHistory.Clone(),
            BatteryPercent = batPct,
            HasBattery = hasBat,
            IsCharging = isCharging,
            BluetoothSummary = _cachedBtSummary,
            HasBluetoothDevice = _hasBtDevice,
            NetworkSummary = netSummary,
            NetworkSpeedSummary = speedSummary,
            CompactDownloadText = compactDown,
            DownloadMbps = downMBps
        };
    }

    private int ReadCpuUsage()
    {
        if (!GetSystemTimes(out FILETIME idleFt, out FILETIME kernelFt, out FILETIME userFt))
        {
            return _lastCpuPercent;
        }

        ulong idle = idleFt.ToUInt64();
        ulong kernel = kernelFt.ToUInt64();
        ulong user = userFt.ToUInt64();

        ulong idleDiff = idle - _prevIdleTime;
        ulong kernelDiff = kernel - _prevKernelTime;
        ulong userDiff = user - _prevUserTime;

        _prevIdleTime = idle;
        _prevKernelTime = kernel;
        _prevUserTime = user;

        ulong totalSys = kernelDiff + userDiff;
        if (totalSys == 0) return _lastCpuPercent;

        double busy = (double)(totalSys - idleDiff) / totalSys * 100.0;
        _lastCpuPercent = (int)Math.Round(Math.Clamp(busy, 0.0, 100.0));
        return _lastCpuPercent;
    }

    private static (int Percent, double UsedGb, double TotalGb) ReadRamUsage()
    {
        try
        {
            var mem = new MEMORYSTATUSEX();
            if (GlobalMemoryStatusEx(mem))
            {
                double totalGb = mem.ullTotalPhys / (1024.0 * 1024.0 * 1024.0);
                double availGb = mem.ullAvailPhys / (1024.0 * 1024.0 * 1024.0);
                double usedGb = Math.Max(0, totalGb - availGb);
                return ((int)mem.dwMemoryLoad, usedGb, totalGb);
            }
        }
        catch
        {
        }
        return (0, 0, 0);
    }

    private static (int Percent, bool HasBattery, bool IsCharging) ReadBatteryStatus()
    {
        try
        {
            var power = SystemInformation.PowerStatus;
            bool noBattery = (power.BatteryChargeStatus & BatteryChargeStatus.NoSystemBattery) != 0
                             || power.BatteryLifePercent > 1.0f;

            if (noBattery)
            {
                return (100, false, true);
            }

            int pct = (int)Math.Round(Math.Clamp(power.BatteryLifePercent * 100f, 0f, 100f));
            bool charging = power.PowerLineStatus == PowerLineStatus.Online
                            || (power.BatteryChargeStatus & BatteryChargeStatus.Charging) != 0;
            return (pct, true, charging);
        }
        catch
        {
            return (100, false, true);
        }
    }

    private async Task RefreshBluetoothStatusAsync()
    {
        try
        {
            string aqs = "System.Devices.Aep.ProtocolId:=\"{e0cbf06c-cd8b-4647-bb8a-263b43f0f974}\" AND System.Devices.Aep.IsConnected:=System.StructuredQueryType.Boolean#True";
            var devices = await DeviceInformation.FindAllAsync(aqs, new[] { "System.Devices.Aep.IsConnected" });

            if (devices != null && devices.Count > 0)
            {
                var names = devices
                    .Select(d => d.Name?.Trim())
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Distinct()
                    .ToList();

                if (names.Count > 0)
                {
                    _hasBtDevice = true;
                    _cachedBtSummary = names.Count == 1
                        ? names[0]!
                        : $"{names[0]} (+{names.Count - 1})";
                    return;
                }
            }

            _hasBtDevice = false;
            _cachedBtSummary = "Bağlı BT cihazı yok";
        }
        catch
        {
            _hasBtDevice = false;
            _cachedBtSummary = "Bluetooth Hazır";
        }
    }

    private static string ReadNetworkStatus()
    {
        try
        {
            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                return "Çevrimdışı";
            }

            var activeNic = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(n =>
                    n.OperationalStatus == OperationalStatus.Up &&
                    n.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                    n.NetworkInterfaceType != NetworkInterfaceType.Tunnel);

            if (activeNic == null) return "Çevrimiçi";

            return activeNic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211
                ? $"Wi-Fi ({activeNic.Name})"
                : $"Ethernet ({activeNic.Name})";
        }
        catch
        {
            return "Çevrimiçi";
        }
    }

    private async Task QueryGpuStatusAsync()
    {
        if ((DateTime.UtcNow - _lastGpuCheckTime).TotalSeconds < 5.0)
        {
            return;
        }
        _lastGpuCheckTime = DateTime.UtcNow;

        if (_hasNvidiaSmi)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "nvidia-smi",
                    Arguments = "--query-gpu=name,utilization.gpu,temperature.gpu,memory.used,memory.total --format=csv,noheader,nounits",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    using var cts = new CancellationTokenSource(1200);
                    string output = await proc.StandardOutput.ReadToEndAsync(cts.Token);
                    await proc.WaitForExitAsync(cts.Token);

                    if (!string.IsNullOrWhiteSpace(output))
                    {
                        var parts = output.Trim().Split(',');
                        if (parts.Length >= 5)
                        {
                            string rawName = parts[0].Trim();
                            _cachedGpuModel = SimplifyGpuName(rawName);

                            if (int.TryParse(parts[1].Trim(), out int pct))
                            {
                                _lastGpuPercent = Math.Clamp(pct, 0, 100);
                            }

                            if (int.TryParse(parts[2].Trim(), out int temp))
                            {
                                _lastGpuTemp = $"{temp}°C";
                            }

                            if (double.TryParse(parts[3].Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double usedMb) &&
                                double.TryParse(parts[4].Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double totalMb))
                            {
                                double usedGb = usedMb / 1024.0;
                                double totalGb = totalMb / 1024.0;
                                _lastGpuMemory = $"{usedGb:0.0} / {totalGb:0.0} GB";
                            }
                            return;
                        }
                    }
                }
            }
            catch
            {
                _hasNvidiaSmi = false;
            }
        }

        // Fallback for Intel/AMD or non-nvidia
        if (string.IsNullOrEmpty(_cachedGpuModel) || _cachedGpuModel == "GPU Ekran Kartı")
        {
            _cachedGpuModel = ReadFallbackGpuModel();
        }
    }

    private static string SimplifyGpuName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "GPU";
        name = name.Replace("NVIDIA GeForce ", "")
                   .Replace("NVIDIA ", "")
                   .Replace("AMD Radeon ", "")
                   .Replace("Intel(R) ", "")
                   .Replace(" Laptop GPU", "")
                   .Replace(" Graphics", "")
                   .Trim();
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", parts.Take(3));
    }

    private static string ReadFallbackGpuModel()
    {
        try
        {
            for (int i = 0; i < 4; i++)
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Control\Class\{{4d36e968-e325-11ce-bfc1-08002be10318}}\000{i}");
                string? desc = key?.GetValue("DriverDesc")?.ToString();
                if (!string.IsNullOrWhiteSpace(desc) && !desc.Contains("Basic Display", StringComparison.OrdinalIgnoreCase))
                {
                    return SimplifyGpuName(desc);
                }
            }
        }
        catch { }
        return "GPU";
    }
}
