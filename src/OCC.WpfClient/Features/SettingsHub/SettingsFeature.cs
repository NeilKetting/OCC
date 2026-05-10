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
            var settings = new NavItem("Settings", string.Empty, "Administration", iconColor: "#FFFF00", iconCode: "\uE713");

            settings.Children.Add(new NavItem(
                "Company Profile",
                NavigationRoutes.CompanyProfile,
                "Administration",
                iconColor: "#0078D4",
                iconCode: "\uE80F"));

            settings.Children.Add(new NavItem(
                "Personal Preferences",
                NavigationRoutes.PersonalPreferences,
                "Personal",
                iconColor: "#E81123",
                iconCode: "\uE779"));

            settings.Children.Add(new NavItem(
                "System Settings",
                NavigationRoutes.CompanySettings,
                "Administration",
                iconColor: "#797775",
                iconCode: "\uE115"));

            yield return settings;
        }
    }
}
