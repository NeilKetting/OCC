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
        public string Icon => "PartnerIcon"; 
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
            var hub = new NavItem("Partner Hub", string.Empty, "Operations", iconColor: "#C0C0C0", iconCode: "\uE8D7");

            hub.Children.Add(new NavItem(
                "Performance Hub",
                NavigationRoutes.PerformanceDashboard,
                "Operations",
                iconColor: "#FF8C00",
                iconCode: "\uE9D2"));

            hub.Children.Add(new NavItem(
                "Sub-Contractors",
                NavigationRoutes.SubContractors,
                "Operations",
                iconColor: "#0078D4",
                iconCode: "\uE77B"));

            hub.Children.Add(new NavItem(
                "Snag List",
                NavigationRoutes.SnagList,
                "Operations",
                iconColor: "#D83B01",
                iconCode: "\uEA37"));

            yield return hub;
        }
    }
}
