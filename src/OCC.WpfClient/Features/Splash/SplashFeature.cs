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
        }

        public IEnumerable<NavItem> GetNavigationItems()
        {
            yield break;
        }
    }
}
