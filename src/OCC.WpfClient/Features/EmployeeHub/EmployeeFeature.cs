using Microsoft.Extensions.DependencyInjection;
using OCC.WpfClient.Features.EmployeeHub.ViewModels;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Services;
using OCC.WpfClient.Services.Interfaces;

namespace OCC.WpfClient.Features.EmployeeHub
{
    public class EmployeeFeature : IFeature
    {
        public string Name => "Employees";
        public int Order => 30; // Part of Admin usually

        public void RegisterServices(IServiceCollection services)
        {
            services.AddSingleton<IEmployeeService, EmployeeService>();
            services.AddTransient<EmployeeListViewModel>();
        }

        public void RegisterRoutes(INavigationService navigationService)
        {
            navigationService.RegisterRoute(NavigationRoutes.StaffManagement, typeof(EmployeeListViewModel));
        }

        public IEnumerable<NavItem> GetNavigationItems()
        {
            var admin = new NavItem("Admin", string.Empty, "Administration", iconColor: "#FFB900", iconCode: "\uE72E");
            admin.Children.Add(new NavItem("Employee Management", NavigationRoutes.StaffManagement, "Administration", iconColor: "#FFB900", iconCode: "\uE77B"));
            yield return admin;
        }
    }
}
