using System;
using Avalonia;
using Serilog;

namespace OCC.Client.Desktop
{
    internal sealed class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args)
        {
            // Enforce South African Culture
            var culture = new System.Globalization.CultureInfo("en-ZA");
            System.Globalization.CultureInfo.DefaultThreadCurrentCulture = culture;
            System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = culture;

            // Configure Serilog
            var logPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                "OCC", "logs", "log-.txt");

            Log.Logger = new LoggerConfiguration()
                .WriteTo.File(logPath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 2)
                .CreateLogger();

            // Global Exception Handlers
            AppDomain.CurrentDomain.UnhandledException += (sender, error) =>
            {
                var ex = error.ExceptionObject as Exception;
                Log.Fatal(ex, "AppDomain Unhandled Exception");
                if (ex != null)
                {
                    ShowFatalError("An unhandled exception occurred.", ex);
                }
                Log.CloseAndFlush();
            };

            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (sender, error) =>
            {
                Log.Error(error.Exception, "TaskScheduler Unobserved Task Exception");
                error.SetObserved();
            };

            try
            {
                Velopack.VelopackApp.Build().Run();

                BuildAvaloniaApp()
                    .StartWithClassicDesktopLifetime(args);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Application StartWithClassicDesktopLifetime Crash");
                ShowFatalError("The application failed to start.", ex);
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern int MessageBox(IntPtr hWnd, String text, String caption, int options);

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();

        private static void ShowFatalError(string message, Exception ex)
        {
            var fullMessage = $"{message}\n\n{ex.Message}\n\nSee log for details.";
            MessageBox(IntPtr.Zero, fullMessage, "Critical Error", 0x10); // 0x10 = MB_ICONHAND (Error)
        }
    }
}
