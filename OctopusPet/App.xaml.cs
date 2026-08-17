using System.IO;
using System.Windows;
using System.Windows.Threading;
using Application = System.Windows.Application;

namespace OctopusPet;

public partial class App : Application
{
    private static readonly string LogPath =
        Path.Combine(AppContext.BaseDirectory, "octopus_pet.log");

    public static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss.fff} {msg}\r\n"); } catch { }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        Log("App.OnStartup");
        DispatcherUnhandledException += (s, args) =>
        {
            Log($"DispatcherUnhandledException: {args.Exception}");
            args.Handled = true;
        };
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log($"App.OnExit code={e.ApplicationExitCode}");
        base.OnExit(e);
    }
}
