using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using FireSprinklerPlugin.SprinkSnap.UI.Shell;

namespace FireSprinklerPlugin.SprinkSnap.WpfPreview;

public partial class App : Application
{
    private static readonly string ErrorLogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SprinkSnap",
        "WpfPreview",
        "startup-error.log");

    public App()
    {
        DispatcherUnhandledException += (_, e) =>
        {
            HandleFatalException(e.Exception, "Unhandled WPF dispatcher exception");
            e.Handled = true;
            Shutdown(-1);
        };

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            HandleFatalException(e.ExceptionObject as Exception, "Unhandled application domain exception");
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            HandleFatalException(e.Exception, "Unobserved task exception");
            e.SetObserved();
        };
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            base.OnStartup(e);

            Window previewWindow = new Window
            {
                Title = "SprinkSnap AI - WPF Preview",
                Width = 1440,
                Height = 860,
                MinWidth = 1180,
                MinHeight = 720,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Content = new SprinkSnapShellView()
            };

            MainWindow = previewWindow;
            previewWindow.Show();
        }
        catch (Exception ex)
        {
            HandleFatalException(ex, "SprinkSnap WPF preview startup failed");
            Shutdown(-1);
        }
    }

    private static void HandleFatalException(Exception exception, string title)
    {
        string message = exception == null
            ? title
            : title + Environment.NewLine + Environment.NewLine + exception;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ErrorLogPath));
            File.WriteAllText(ErrorLogPath, message);
        }
        catch
        {
            // If logging fails, still show the startup exception to the user.
        }

        MessageBox.Show(
            message + Environment.NewLine + Environment.NewLine + "Log file: " + ErrorLogPath,
            title,
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}

