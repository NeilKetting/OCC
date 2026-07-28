using Microsoft.Extensions.DependencyInjection;
using OCC.WpfClient.Features.AdminHub.Users.ViewModels;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Services;
using OCC.WpfClient.Services.Interfaces;

namespace OCC.WpfClient.Features.AdminHub
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
            var admin = new NavItem("Admin", string.Empty, "Administration", iconColor: "#FFB900", iconCode: "\uE72E");
            admin.Children.Add(new NavItem("User Management", NavigationRoutes.UserManagement, "Administration", iconColor: "#FFB900", iconCode: "\uE77B"));
            
            yield return admin;
        }
    }
}
