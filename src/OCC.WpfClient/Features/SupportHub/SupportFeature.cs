using Microsoft.Extensions.DependencyInjection;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using OCC.WpfClient.Services;
using OCC.WpfClient.Features.SupportHub.ViewModels;
using System.Collections.Generic;

namespace OCC.WpfClient.Features.SupportHub
{
    public class SupportFeature : IFeature
    {
        public string Name => "Support";
        public int Order => 1000;

        public void RegisterServices(IServiceCollection services)
        {
            services.AddSingleton<IBugReportService, BugReportService>();
            services.AddTransient<SupportViewModel>();
            services.AddTransient<ReportBugViewModel>();
        }

        public void RegisterRoutes(INavigationService navigationService)
        {
            navigationService.RegisterRoute("Support.SupportHub", typeof(SupportViewModel));
            navigationService.RegisterRoute("Support.ReportBug", typeof(ReportBugViewModel));
        }

        public IEnumerable<NavItem> GetNavigationItems()
        {
            yield break;
        }
    }
}
