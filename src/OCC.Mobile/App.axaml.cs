using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using OCC.Mobile.Features.AdminDashboard;
using OCC.Mobile.Features.Dashboard;
using OCC.Mobile.Features.Login;
using OCC.Mobile.Features.Notifications;
using OCC.Mobile.Features.Shell;
using OCC.Mobile.Services;
using OCC.Mobile.ViewModels;
using System;
using Serilog;
using System.IO;

namespace OCC.Mobile
{
    public partial class App : Application
    {
        public IServiceProvider? Services { get; private set; }

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            try
            {
                var serviceCollection = new ServiceCollection();
                ConfigureServices(serviceCollection);
                Services = serviceCollection.BuildServiceProvider();

                var mainViewModel = Services.GetRequiredService<MainViewModel>();
                var navigationService = Services.GetRequiredService<INavigationService>();

                if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    desktop.MainWindow = new Window
                    {
                        Title = "OCC Mobile (Tablet View)",
                        Width = 1280,
                        Height = 800,
                        WindowStartupLocation = WindowStartupLocation.CenterScreen,
                        Content = new OCC.Mobile.Features.Shell.MainView
                        {
                            DataContext = mainViewModel
                        }
                    };

                    navigationService.NavigateTo<LoginViewModel>();
                }
                else if (ApplicationLifetime is IActivityApplicationLifetime activityLifetime)
                {
                    activityLifetime.MainViewFactory = () => new OCC.Mobile.Features.Shell.MainView
                    {
                        DataContext = mainViewModel
                    };
                    
                    navigationService.NavigateTo<LoginViewModel>();
                }
                else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
                {
                    singleViewPlatform.MainView = new OCC.Mobile.Features.Shell.MainView
                    {
                        DataContext = mainViewModel
                    };

                    navigationService.NavigateTo<LoginViewModel>();
                }

                base.OnFrameworkInitializationCompleted();
            }
            catch (Exception ex)
            {
                // This might not show up on Android unless we have a logger, 
                // but we can catch it in the debugger.
                System.Diagnostics.Debug.WriteLine($"BOOT ERROR: {ex.Message}");
                throw;
            }
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // Configure Serilog for file logging
            var logPath = Path.Combine(AppContext.BaseDirectory, "logs", "mobile-log-.txt");
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.Console()
                .WriteTo.Debug()
                .WriteTo.File(logPath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 2)
                .CreateLogger();

            services.AddLogging(l => l.AddSerilog());

            // ViewModels
            services.AddSingleton<MainViewModel>();
            services.AddTransient<LoginViewModel>();
            services.AddTransient<DashboardViewModel>();
            services.AddTransient<AdminDashboardViewModel>();
            services.AddTransient<ActiveProjectsViewModel>();
            services.AddTransient<OverdueTasksViewModel>();
            services.AddTransient<MyTasksViewModel>();
            services.AddTransient<TaskDetailViewModel>();
            
            // Services
            services.AddSingleton<ILocalSettingsService, LocalSettingsService>();
            services.AddSingleton<IAuthService, AuthService>();
            services.AddSingleton<INavigationService, NavigationService>();
            services.AddSingleton<IPushNotificationService, PushNotificationService>();
            services.AddSingleton<IProjectTaskService, ProjectTaskService>();
            services.AddSingleton<IProjectService, ProjectService>();
            services.AddSingleton<IHseqService, HseqService>();
            services.AddSingleton<ISignalRService, SignalRService>();
            services.AddSingleton<Func<MainViewModel>>(s => () => s.GetRequiredService<MainViewModel>());
        }
    }
}
