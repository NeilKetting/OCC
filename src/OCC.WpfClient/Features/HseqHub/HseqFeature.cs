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
            services.AddTransient<IncidentDetailViewModel>();
            services.AddTransient<TrainingViewModel>();
            services.AddTransient<TrainingDetailViewModel>();
            services.AddTransient<AuditsViewModel>();
            services.AddTransient<AuditDetailViewModel>();
            services.AddTransient<DeviationDetailViewModel>();
            services.AddTransient<PerformanceMonitoringViewModel>();
            services.AddTransient<DocumentsViewModel>();
            services.AddTransient<DocumentDetailViewModel>();
            services.AddTransient<HealthSafetyViewModel>();
        }

        public void RegisterRoutes(INavigationService navigationService)
        {
            navigationService.RegisterRoute(NavigationRoutes.HealthSafety, typeof(HealthSafetyViewModel));
        }

        public IEnumerable<NavItem> GetNavigationItems()
        {
            var hseq = new NavItem("HSEQ Hub", NavigationRoutes.HealthSafety, "HSEQ", iconColor: "#00FFFF", iconCode: "\uEA18");


            yield return hseq;
        }
    }
}
