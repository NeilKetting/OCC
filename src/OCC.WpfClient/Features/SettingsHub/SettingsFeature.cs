using Microsoft.Extensions.DependencyInjection;
using OCC.WpfClient.Features.SettingsHub.ViewModels;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using OCC.WpfClient.Services;
using System.Collections.Generic;

namespace OCC.WpfClient.Features.SettingsHub
{
    public class SettingsFeature : IFeature
    {
        public string Name => "Settings";
        public string Description => "Company configuration and system preferences";
        public string Icon => "IconGear";
        public int Order => 1000;

        public void RegisterServices(IServiceCollection services)
        {
            services.AddSingleton<ISettingsService, SettingsService>();
            services.AddTransient<CompanyProfileViewModel>();
            services.AddTransient<CompanySettingsViewModel>();
            services.AddTransient<PersonalPreferencesViewModel>();
        }

        public void RegisterRoutes(INavigationService navigationService)
        {
            navigationService.RegisterRoute(NavigationRoutes.CompanyProfile, typeof(CompanyProfileViewModel));
            navigationService.RegisterRoute(NavigationRoutes.CompanySettings, typeof(CompanySettingsViewModel));
            navigationService.RegisterRoute(NavigationRoutes.PersonalPreferences, typeof(PersonalPreferencesViewModel));
        }

        public IEnumerable<NavItem> GetNavigationItems()
        {
            var settings = new NavItem("Settings", "IconGear", string.Empty, "Administration");

            settings.Children.Add(new NavItem(
                "Company Profile",
                "IconHome",
                NavigationRoutes.CompanyProfile,
                "Administration"));

            settings.Children.Add(new NavItem(
                "Personal Preferences",
                "IconUser",
                NavigationRoutes.PersonalPreferences,
                "Personal"));

            settings.Children.Add(new NavItem(
                "System Settings",
                "IconSettings",
                NavigationRoutes.CompanySettings,
                "Administration"));

            yield return settings;
        }
    }
}
