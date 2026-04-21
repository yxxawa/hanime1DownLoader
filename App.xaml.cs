using Hanime1Downloader.CSharp.Services;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace Hanime1Downloader.CSharp;

public partial class App : Application
{
    private static readonly string CrashLogPath = Path.Combine(AppContext.BaseDirectory, "crash.log");

    protected override void OnStartup(StartupEventArgs e)
    {
        AppTheme.Apply(this, AppTheme.ReadSavedThemeMode());
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            File.AppendAllText(CrashLogPath, $"[{DateTime.Now}] DispatcherUnhandledException:\n{args.Exception}\n\n");
            AppLogger.Error("crash", "DispatcherUnhandledException", args.Exception);
            args.Handled = false;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            File.AppendAllText(CrashLogPath, $"[{DateTime.Now}] UnhandledException:\n{args.ExceptionObject}\n\n");
            AppLogger.Error("crash", "UnhandledException", args.ExceptionObject as Exception);
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            File.AppendAllText(CrashLogPath, $"[{DateTime.Now}] UnobservedTaskException:\n{args.Exception}\n\n");
            AppLogger.Error("crash", "UnobservedTaskException", args.Exception);
            args.SetObserved();
        };
    }
}
