using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using OCC.Mobile.ViewModels;
using OCC.Mobile.ViewModels.Login;
using OCC.Mobile.ViewModels.Dashboard;
using OCC.Mobile.Services;
using System;

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

                if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
                {
                    singleViewPlatform.MainView = new MainView
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
            // ViewModels
            services.AddSingleton<MainViewModel>();
            services.AddTransient<LoginViewModel>();
            services.AddTransient<DashboardViewModel>();
            
            // Services
            services.AddSingleton<ILocalSettingsService, LocalSettingsService>();
            services.AddSingleton<INavigationService, NavigationService>();
            services.AddSingleton<Func<MainViewModel>>(s => () => s.GetRequiredService<MainViewModel>());
        }
    }
}
