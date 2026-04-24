using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using System.Globalization;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using OCC.Client.Services;
using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Infrastructure; // Restored
using OCC.Client.Infrastructure.DependencyInjection; // Added
using OCC.Client.ViewModels.Core;
using OCC.Client.Mobile.Shell;
using OCC.Client.Mobile.Features.Auth.Login;
using OCC.Shared.Models;
using Serilog;
using System;
using System.Linq;
using LiveChartsCore; 
using LiveChartsCore.SkiaSharpView; 
using OCC.Client.Views.Core;
using OCC.Client.Features.CoreHub.Views;
using OCC.Client.Features.AuthHub.Views;
using OCC.Client.Features.AuthHub.ViewModels;
using CommunityToolkit.Mvvm.Messaging;
using OCC.Client.ViewModels.Messages;

namespace OCC.Client
{
    public partial class App : Application
    {
        public IServiceProvider? Services { get; private set; }
        public static Action<IServiceCollection>? RegisterPlatformServices { get; set; }

        public override void Initialize()
        {
            // Force dot decimal separator globally for consistency (e.g. for South African locale)
            var culture = (CultureInfo)CultureInfo.CurrentCulture.Clone();
            culture.NumberFormat.NumberDecimalSeparator = ".";
            culture.NumberFormat.CurrencyDecimalSeparator = ".";
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;

            AvaloniaXamlLoader.Load(this);

            LiveCharts.Configure(config => 
                config
                     .AddSkiaSharp()
                     .AddDefaultMappers()
                     .AddLightTheme()
            );
        }

        public static void ChangeTheme(bool isDarkMode)
        {
            if (Current == null) 
            {
                System.Diagnostics.Debug.WriteLine("ChangeTheme: Current Application is null!");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"ChangeTheme: Switching to {(isDarkMode ? "Dark" : "Light")}");

            var merged = Current.Resources.MergedDictionaries;
            merged.Clear();
            
            var newTheme = isDarkMode 
                ? new Avalonia.Markup.Xaml.Styling.ResourceInclude(new Uri("avares://OCC.Client/Styles/Themes/DarkMode.axaml")) { Source = new Uri("avares://OCC.Client/Styles/Themes/DarkMode.axaml") }
                : new Avalonia.Markup.Xaml.Styling.ResourceInclude(new Uri("avares://OCC.Client/Styles/Themes/LightMode.axaml")) { Source = new Uri("avares://OCC.Client/Styles/Themes/LightMode.axaml") };
            
            try
            {
                merged.Add(newTheme);
            }
            catch (ArgumentException ex) { System.Diagnostics.Debug.WriteLine($"Error changing theme: {ex.Message}"); }
            System.Diagnostics.Debug.WriteLine("ChangeTheme: Theme switched.");
        }

        public override void OnFrameworkInitializationCompleted()
        {
            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);
            Services = serviceCollection.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                DisableAvaloniaDataAnnotationValidation();
                
                var shellViewModel = Services.GetRequiredService<MainViewModel>(); 
                var updateService = Services.GetRequiredService<IUpdateService>();
                
                SplashView? splashWindow = null;

                var splashVm = new OCC.Client.Features.CoreHub.ViewModels.SplashViewModel(updateService, () =>
                {
                    var mainWindow = new MainWindow
                    {
                        DataContext = shellViewModel
                    };
                    
                    var activityService = Services.GetRequiredService<UserActivityService>();
                    activityService.Monitor(mainWindow);

                    desktop.MainWindow = mainWindow;
                    mainWindow.Show();
                    
                    splashWindow?.Close();
                });

                splashWindow = new SplashView
                {
                    DataContext = splashVm
                };
                
                desktop.MainWindow = splashWindow;
            }
            else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
            {
                // On Mobile/SingleView, we start with the Login View
                singleViewPlatform.MainView = new MobileLoginView
                {
                    DataContext = Services.GetRequiredService<MobileLoginViewModel>()
                };

                // Handle navigation to the Mobile Hub after successful login
                WeakReferenceMessenger.Default.Register<NavigationMessage>(this, (r, m) =>
                {
                    if (m.Value is MobileHubViewModel mobileHubVm)
                    {
                        singleViewPlatform.MainView = new OCC.Client.Mobile.Shell.MobileHubView
                        {
                            DataContext = mobileHubVm
                        };
                    }
                });
            }

            base.OnFrameworkInitializationCompleted();
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging(l => l.AddSerilog());

            // Modular Service Registrations
            services.AddInfrastructureServices()
                    .AddAuthHub()
                    .AddHomeHub()
                    .AddProjectsHub()
                    .AddEmployeeHub()
                    .AddWagesHub()
                    .AddOrdersHub()
                    .AddHseqHub()
                    .AddSupportHub()
                    .AddSettingsHub()
                    .AddMobileHub();

            RegisterPlatformServices?.Invoke(services);
        }

        private void DisableAvaloniaDataAnnotationValidation()
        {
            var dataValidationPluginsToRemove =
                BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

            foreach (var plugin in dataValidationPluginsToRemove)
            {
                BindingPlugins.DataValidators.Remove(plugin);
            }
        }
    }
}
