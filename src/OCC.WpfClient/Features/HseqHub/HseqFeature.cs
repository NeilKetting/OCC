using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using OCC.WpfClient.Services;
using OCC.WpfClient.Features.HseqHub.ViewModels;

namespace OCC.WpfClient.Features.HseqHub
{
    public class HseqFeature : IFeature
    {
        public string Name => "HSEQ Hub";
        public int Order => 40;

        public void RegisterServices(IServiceCollection services)
        {
            // Service
            services.AddSingleton<IHealthSafetyService, HealthSafetyService>();

            // ViewModels
            services.AddTransient<HealthSafetyMenuViewModel>();
            services.AddTransient<HealthSafetyDashboardViewModel>();
            services.AddTransient<IncidentsViewModel>();
            services.AddTransient<IncidentEditorViewModel>();
            services.AddTransient<TrainingViewModel>();
            services.AddTransient<TrainingEditorViewModel>();
            services.AddTransient<AuditsViewModel>();
            services.AddTransient<AuditEditorViewModel>();
            services.AddTransient<AuditDeviationsViewModel>();
            services.AddTransient<PerformanceMonitoringViewModel>();
            services.AddTransient<DocumentsViewModel>();
            services.AddTransient<HealthSafetyViewModel>();
        }

        public void RegisterRoutes(INavigationService navigationService)
        {
            navigationService.RegisterRoute(NavigationRoutes.HealthSafety, typeof(HealthSafetyViewModel));
        }

        public IEnumerable<NavItem> GetNavigationItems()
        {
            yield return new NavItem("HSEQ Hub", "IconHealthSafety", NavigationRoutes.HealthSafety, "HSEQ");
        }
    }
}
