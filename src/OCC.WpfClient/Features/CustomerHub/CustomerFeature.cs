using Microsoft.Extensions.DependencyInjection;
using OCC.WpfClient.Features.CustomerHub.ViewModels;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Services;
using OCC.WpfClient.Services.Interfaces;

namespace OCC.WpfClient.Features.CustomerHub
{
    public class CustomerFeature : IFeature
    {
        public string Name => "Customers";
        public int Order => 20;

        public void RegisterServices(IServiceCollection services)
        {
            services.AddSingleton<ICustomerService, CustomerService>();
            services.AddTransient<CustomerListViewModel>();
            services.AddTransient<CustomerDetailViewModel>();
        }

        public void RegisterRoutes(INavigationService navigationService)
        {
            navigationService.RegisterRoute(NavigationRoutes.Customers, typeof(CustomerListViewModel));
        }

        public IEnumerable<NavItem> GetNavigationItems()
        {
            yield return new NavItem("Customers", "IconAddUser", NavigationRoutes.Customers, "Main");
        }
    }
}
