using Microsoft.Extensions.DependencyInjection;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using OCC.WpfClient.Features.Splash.ViewModels;
using System.Collections.Generic;

namespace OCC.WpfClient.Features.Splash
{
    public class SplashFeature : IFeature
    {
        public string Name => "Splash";
        public int Order => -2;

        public void RegisterServices(IServiceCollection services)
        {
            services.AddTransient<SplashViewModel>();
        }

        public void RegisterRoutes(INavigationService navigationService)
        {
            navigationService.RegisterRoute("Splash", typeof(SplashViewModel));
            navigationService.RegisterRoute(NavigationRoutes.Home, typeof(OCC.WpfClient.Features.Main.ViewModels.DashboardViewModel));
        }

        public IEnumerable<NavItem> GetNavigationItems()
        {
            yield return new NavItem(
                label:     "Dashboard",
                route:     NavigationRoutes.Home,
                category:  "Workspace",
                iconColor: "#00FF88",
                iconCode:  "\uE80F");
        }
    }
}
