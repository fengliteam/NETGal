using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace NETGal.Studio.Windows;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += (_, args) =>
        {
            LogException(args.Exception);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) => LogException(args.ExceptionObject as Exception);
        base.OnStartup(e);
        MainWindow = new MainWindow();
        MainWindow.Show();
        MainWindow.Activate();
    }

    private static void LogException(Exception? exception)
    {
        try
        {
            var root = AppContext.BaseDirectory;
            File.AppendAllText(Path.Combine(root, "netgal-studio-windows.log"), $"{DateTimeOffset.Now:O} {exception}\n");
        }
        catch
        {
        }
    }
}
