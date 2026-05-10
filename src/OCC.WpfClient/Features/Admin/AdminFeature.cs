using Microsoft.Extensions.DependencyInjection;
using OCC.WpfClient.Features.Admin.Users.ViewModels;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Services;
using OCC.WpfClient.Services.Interfaces;

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
            var admin = new NavItem("Admin", string.Empty, "Administration", iconColor: "#FFA500", iconCode: "\uE72E");
            admin.Children.Add(new NavItem("User Management", NavigationRoutes.UserManagement, "Administration", iconColor: "#FFA500", iconCode: "\uE77B"));
            
            yield return admin;
        }
    }
}
