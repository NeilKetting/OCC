using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OCC.WpfClient.Services;
using OCC.WpfClient.Services.Interfaces;
using OCC.WpfClient.Features.AuthHub;
using OCC.WpfClient.Features.Splash;
using OCC.WpfClient.Features.Main.ViewModels;
using OCC.WpfClient.Services.Infrastructure;
using OCC.WpfClient.Services.Infrastructure.Logging;
using OCC.WpfClient.Features.EmployeeHub;
using OCC.WpfClient.Features.Admin;
using OCC.WpfClient.Features.SupportHub;
using OCC.WpfClient.Features.ChatHub;
using OCC.WpfClient.Features.ProcurementHub;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Features.Splash.ViewModels;
using OCC.WpfClient.Features.AuthHub.ViewModels;
using OCC.WpfClient.Features.Shell.ViewModels;
using OCC.WpfClient.Features.CustomerHub;
using OCC.WpfClient.Features.SettingsHub;
using OCC.WpfClient.Features.ProjectHub;
using OCC.WpfClient.Features.SubContractorHub;
using OCC.Shared.Interfaces;
using OCC.Shared.Services;
using System.Net.Http;
using OCC.WpfClient.Features.HseqHub;



namespace OCC.WpfClient
{
    public partial class App : Application
    {
        public IServiceProvider ServiceProvider { get; private set; } = null!;

        protected override async void OnStartup(StartupEventArgs e)
        {
            // Set Culture to South Africa for Rands (R) currency formatting
            var culture = new CultureInfo("en-ZA");
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;

            // Ensure WPF uses the same culture for bindings by default
            FrameworkElement.LanguageProperty.OverrideMetadata(
                typeof(FrameworkElement),
                new FrameworkPropertyMetadata(
                    System.Windows.Markup.XmlLanguage.GetLanguage(culture.IetfLanguageTag)));

            // Global Exception Handling
            this.DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

            base.OnStartup(e);

            var services = new ServiceCollection();
            ConfigureServices(services);
            ServiceProvider = services.BuildServiceProvider();

            // Initialize Routes
            var navService = ServiceProvider.GetRequiredService<INavigationService>();
            foreach (var feature in ServiceProvider.GetServices<IFeature>())
            {
                feature.RegisterRoutes(navService);
            }

            // Show Splash Screen
            var splashViewModel = ServiceProvider.GetRequiredService<SplashViewModel>();
            var splashView = ServiceProvider.GetRequiredService<Features.Splash.Views.SplashView>();
            splashView.DataContext = splashViewModel;
            
            this.MainWindow = splashView;
            splashView.Show();

            // Perform Initialization
            await splashViewModel.InitializeAsync();

            // Show Main Window
            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            this.MainWindow = mainWindow;
            mainWindow.Show();
            
            splashView.Close();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // Logging
            services.AddLogging(builder =>
            {
                builder.AddDebug();
                builder.AddConsole();
                builder.AddWpfFileLogger();
            });

            // Infrastructure
            services.AddSingleton<ConnectionSettings>();
            services.AddSingleton<LocalSettingsService>();
            services.AddSingleton<ILocalEncryptionService, LocalEncryptionService>();
            services.AddSingleton<UserActivityService>();

            // Services
            services.AddHttpClient();
            services.AddTransient<IGoogleMapsService>(sp => 
            {
                var factory = sp.GetRequiredService<IHttpClientFactory>();
                var client = factory.CreateClient();
                var settings = sp.GetRequiredService<ConnectionSettings>();
                return new GoogleMapsService(client, settings.GoogleApiKey);
            });
            services.AddSingleton<INavigationService, NavigationService>();
            services.AddSingleton<IPermissionService, PermissionService>();
            services.AddSingleton<IUpdateService, UpdateService>();
            services.AddSingleton<IToastService, ToastService>();
            services.AddSingleton<ISignalRService, SignalRService>();
            services.AddSingleton<IDialogService, WpfDialogService>();
            services.AddSingleton<IInventoryService, InventoryService>();
            services.AddSingleton<IProjectTaskService, ProjectTaskService>();
            services.AddSingleton<ITaskAssignmentService, TaskAssignmentService>();
            services.AddSingleton<ITaskCommentService, TaskCommentService>();
            services.AddSingleton<ITaskAttachmentService, TaskAttachmentService>();
            services.AddSingleton<IPdfService, PdfService>();

            // Feature Discovery
            var features = new List<IFeature>
            {
                new SplashFeature(),
                new AuthFeature(),
                new ChatFeature(),
                new EmployeeFeature(),
                new AdminFeature(),
                new SupportFeature(),
                new CustomerFeature(),
                new ProcurementFeature(),
                new ProjectFeature(),
                new SubContractorFeature(),
                new SettingsFeature(),
                new HseqFeature()
            };



            foreach (var feature in features)
            {
                feature.RegisterServices(services);
                services.AddSingleton<IFeature>(feature);
            }
            services.AddSingleton<IFeatureService, FeatureService>();

            // Shell ViewModels
            services.AddTransient<ShellViewModel>();
            services.AddTransient<MainViewModel>();
            services.AddTransient<ProfileViewModel>();
            services.AddTransient<DashboardViewModel>();

            // Windows
            services.AddSingleton<MainWindow>();
            services.AddSingleton<Features.Splash.Views.SplashView>();
        }

        private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            LogException(e.Exception, "DispatcherUnhandledException");
            // e.Handled = true; // Don't handle it, let it crash but log it first
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                LogException(ex, "AppDomain.UnhandledException");
            }
        }

        private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            LogException(e.Exception, "TaskScheduler.UnobservedTaskException");
            e.SetObserved();
        }

        private void LogException(Exception? ex, string source)
        {
            if (ex == null) return;

            try
            {
                var logger = ServiceProvider?.GetService<ILogger<App>>();
                if (logger != null)
                {
                    logger.LogCritical(ex, "FATAL UNHANDLED EXCEPTION [{Source}]: {Message}", source, ex.Message);
                    
                    // Log Inner Exception if exists
                    if (ex.InnerException != null)
                    {
                        logger.LogCritical(ex.InnerException, "Inner Exception for {Source}: {Message}", source, ex.InnerException.Message);
                    }
                }
                else
                {
                    // Fallback if logger is not yet available
                    System.Diagnostics.Debug.WriteLine($"FATAL: {source}: {ex}");
                }
            }
            catch
            {
                // Last resort
                System.Diagnostics.Debug.WriteLine($"Error logging exception from {source}");
            }
        }
    }
}
