using Microsoft.Extensions.DependencyInjection;
using OCC.WpfClient.Features.HseqHub.ViewModels;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Services;
using OCC.WpfClient.Services.Interfaces;

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
            services.AddTransient<IncidentListViewModel>();
            services.AddTransient<IncidentDetailViewModel>();
            services.AddTransient<TrainingListViewModel>();
            services.AddTransient<TrainingDetailViewModel>();
            services.AddTransient<AuditListViewModel>();
            services.AddTransient<AuditDetailViewModel>();
            services.AddTransient<AuditPdfMappingViewModel>();
            services.AddTransient<DeviationDetailViewModel>();
            services.AddTransient<PerformanceMonitoringListViewModel>();
            services.AddTransient<DocumentsListViewModel>();
            services.AddTransient<DocumentDetailViewModel>();
            services.AddTransient<HealthSafetyViewModel>();
        }

        public void RegisterRoutes(INavigationService navigationService)
        {
            navigationService.RegisterRoute(NavigationRoutes.HealthSafety, typeof(HealthSafetyViewModel));
        }

        public IEnumerable<NavItem> GetNavigationItems()
        {
            var hseq = new NavItem("HSEQ", NavigationRoutes.HealthSafety, "HSEQ", iconColor: "#00FFFF", iconCode: "\uEA18");


            yield return hseq;
        }
    }
}
