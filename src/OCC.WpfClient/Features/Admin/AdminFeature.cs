using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using OCC.WpfClient.Services;
using Microsoft.Extensions.DependencyInjection;
using OCC.WpfClient.Features.Admin.Users.ViewModels;
using System.Collections.Generic;

namespace OCC.WpfClient.Features.Admin
{
    public class AdminFeature : IFeature
    {
        public string Name => "Administration";
        public int Order => 100;

        public void RegisterServices(IServiceCollection services)
        {
            services.AddSingleton<IUserService, UserService>();
            services.AddTransient<UserListViewModel>();
            services.AddTransient<UserDetailViewModel>();
        }

        public void RegisterRoutes(INavigationService navigationService)
        {
            navigationService.RegisterRoute(NavigationRoutes.UserManagement, typeof(UserListViewModel));
        }

        public IEnumerable<NavItem> GetNavigationItems()
        {
            var admin = new NavItem("Admin", "IconGear", string.Empty, "Administration");
            admin.Children.Add(new NavItem("User Management", "IconTeam", NavigationRoutes.UserManagement, "Administration"));
            
            yield return admin;
        }
    }
}
