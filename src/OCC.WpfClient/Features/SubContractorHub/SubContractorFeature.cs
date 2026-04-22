using Microsoft.Extensions.DependencyInjection;
using OCC.WpfClient.Features.SubContractorHub.ViewModels;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using OCC.WpfClient.Services;
using System.Collections.Generic;

namespace OCC.WpfClient.Features.SubContractorHub
{
    public class SubContractorFeature : IFeature
    {
        public string Name => "Partners";
        public string Description => "Sub-Contractor Performance and Snag Management";
        public string Icon => "IconTeam"; 
        public int Order => 40;

        public void RegisterServices(IServiceCollection services)
        {
            services.AddSingleton<ISubContractorService, SubContractorService>();
            services.AddSingleton<ISnagService, SnagService>();
            
            services.AddTransient<SubContractorListViewModel>();
            services.AddTransient<SubContractorDetailViewModel>();
            services.AddTransient<PerformanceDashboardViewModel>();
            services.AddTransient<SnagListViewModel>();
            services.AddTransient<SnagDetailViewModel>();
        }

        public void RegisterRoutes(INavigationService navigationService)
        {
            navigationService.RegisterRoute(NavigationRoutes.SubContractors, typeof(SubContractorListViewModel));
            navigationService.RegisterRoute(NavigationRoutes.PerformanceDashboard, typeof(PerformanceDashboardViewModel));
            navigationService.RegisterRoute(NavigationRoutes.SnagList, typeof(SnagListViewModel));
        }

        public IEnumerable<NavItem> GetNavigationItems()
        {
            var hub = new NavItem("Partner Hub", "IconTeam", string.Empty, "Operations");

            hub.Children.Add(new NavItem(
                "Performance Hub",
                "IconActivity",
                NavigationRoutes.PerformanceDashboard,
                "Operations"));

            hub.Children.Add(new NavItem(
                "Sub-Contractors",
                "IconTeam",
                NavigationRoutes.SubContractors,
                "Operations"));

            hub.Children.Add(new NavItem(
                "Snag List",
                "IconList",
                NavigationRoutes.SnagList,
                "Operations"));

            yield return hub;
        }
    }
}
