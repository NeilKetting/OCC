using Microsoft.Extensions.DependencyInjection;
using OCC.WpfClient.Services.Interfaces;

namespace OCC.WpfClient.Infrastructure
{
    public interface IFeature
    {
        string Name { get; }
        int Order { get; } 
        void RegisterServices(IServiceCollection services);
        void RegisterRoutes(INavigationService navigationService);
        IEnumerable<NavItem> GetNavigationItems();
    }
}
