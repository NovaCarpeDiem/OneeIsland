using System.IO;
using System.Threading;
using System.Windows;

namespace DynamicIslandWin;

public partial class App : System.Windows.Application
{
    private static Mutex? _mutex;
    private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup.log");





    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += (s, args) =>
        {
            File.AppendAllText(LogPath, $"[DispatcherUnhandled] {args.Exception}\n");
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            File.AppendAllText(LogPath, $"[DomainUnhandled] {args.ExceptionObject}\n");
        };

        TaskScheduler.UnobservedTaskException += (s, args) =>
        {
            File.AppendAllText(LogPath, $"[TaskUnhandled] {args.Exception}\n");
            args.SetObserved();
        };

        const string mutexName = "DynamicIslandWin_SingleInstance_Mutex_v2";
        _mutex = new Mutex(true, mutexName, out bool createdNew);

        if (!createdNew)
        {
            File.AppendAllText(LogPath, "[Info] Mutex already held, terminating previous instance to launch fresh.\n");
            try
            {
                var currentPid = Environment.ProcessId;
                foreach (var p in System.Diagnostics.Process.GetProcessesByName("DynamicIslandWin"))
                {
                    if (p.Id != currentPid)
                    {
                        p.Kill();
                        p.WaitForExit(1500);
                    }
                }
            }
            catch { }

            try
            {
                _mutex = new Mutex(true, mutexName, out createdNew);
            }
            catch { }
        }

        base.OnStartup(e);

        try
        {
            System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.Default;
            try
            {
                System.Windows.Media.Animation.Timeline.DesiredFrameRateProperty.OverrideMetadata(
                    typeof(System.Windows.Media.Animation.Timeline),
                    new FrameworkPropertyMetadata { DefaultValue = 144 });
            }
            catch { }

            var win = new MainWindow();
            MainWindow = win;
            win.Closed += (_, _) =>
            {
                File.AppendAllText(LogPath, "[Info] MainWindow closed.\n");
                Shutdown();
            };
            win.Show();
            File.AppendAllText(LogPath, "[Info] MainWindow shown successfully.\n");
        }
        catch (Exception ex)
        {
            File.AppendAllText(LogPath, $"[StartupError] {ex}\n");
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        File.AppendAllText(LogPath, $"[Info] OnExit called with exit code: {e.ApplicationExitCode}\n");
        try
        {
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
        }
        catch
        {
        }
        base.OnExit(e);
    }
}
