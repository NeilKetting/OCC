using Microsoft.Extensions.DependencyInjection;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using OCC.WpfClient.Services;
using OCC.WpfClient.Features.AuthHub.ViewModels;
using System.Collections.Generic;

namespace OCC.WpfClient.Features.AuthHub
{
    public class AuthFeature : IFeature
    {
        public string Name => "Authentication";
        public int Order => -1; // Not in sidebar

        public void RegisterServices(IServiceCollection services)
        {
            services.AddSingleton<IAuthService, AuthService>();
            services.AddTransient<AuthViewModel>();
        }

        public void RegisterRoutes(INavigationService navigationService)
        {
            navigationService.RegisterRoute("Auth", typeof(AuthViewModel));
        }

        public IEnumerable<NavItem> GetNavigationItems()
        {
            // Auth usually doesn't have a sidebar item, 
            // but it needs a route.
            yield break;
        }
    }
}
