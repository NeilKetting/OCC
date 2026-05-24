using Microsoft.Extensions.DependencyInjection;
using OCC.WpfClient.Features.ProjectHub.ViewModels;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using OCC.WpfClient.Services;
using System.Collections.Generic;

namespace OCC.WpfClient.Features.ProjectHub
{
    public class ProjectFeature : IFeature
    {
        public string Name => "Projects";
        public string Description => "Construction Project Management and Portfolio Tracking";
        public string Icon => "IconPortfolio"; 
        public int Order => 30;

        public void RegisterServices(IServiceCollection services)
        {
            services.AddTransient<ProjectDashboardViewModel>();
            services.AddTransient<ProjectListViewModel>();
            services.AddTransient<ProjectDetailViewModel>();
            services.AddTransient<ProjectSpecificDashboardViewModel>();
            services.AddTransient<ProjectTaskListViewModel>();
            services.AddTransient<TaskDetailViewModel>();
            services.AddTransient<CreateProjectViewModel>();
            services.AddTransient<ProjectEditorViewModel>();
            services.AddTransient<ProjectGanttViewModel>();
            services.AddTransient<ProjectHistoryViewModel>();
            services.AddTransient<ProjectReportViewModel>();
            services.AddTransient<ProjectReportRunViewModel>();
            services.AddTransient<ProjectVariationOrderListViewModel>();
            services.AddTransient<ProjectVariationOrderDetailViewModel>();
            services.AddTransient<IProjectVariationOrderService, ProjectVariationOrderService>();
            services.AddTransient<IProjectReportService, ProjectReportService>();
        }
 
        public void RegisterRoutes(INavigationService navigationService)
        {
            navigationService.RegisterRoute(NavigationRoutes.ProjectDashboard, typeof(ProjectDashboardViewModel));
            navigationService.RegisterRoute(NavigationRoutes.Projects, typeof(ProjectListViewModel));
            navigationService.RegisterRoute(NavigationRoutes.ProjectDetail, typeof(ProjectDetailViewModel));
            navigationService.RegisterRoute(NavigationRoutes.ProjectReportRun, typeof(ProjectReportRunViewModel));
        }
 
        public IEnumerable<NavItem> GetNavigationItems()
        {
            var projects = new NavItem("Projects", string.Empty, "Operations", iconColor: "#800080", iconCode: "\uE838");
 
            projects.Children.Add(new NavItem(
                "Project Dashboard",
                NavigationRoutes.ProjectDashboard,
                "Operations",
                iconColor: "#107C10",
                iconCode: "\uE9D9"));
 
            projects.Children.Add(new NavItem(
                "Projects",
                NavigationRoutes.Projects,
                "Operations",
                iconColor: "#5C2D91",
                iconCode: "\uEA37"));

            projects.Children.Add(new NavItem(
                "Report Run",
                NavigationRoutes.ProjectReportRun,
                "Operations",
                iconColor: "#EF6C00",
                iconCode: "\uE9F9"));

            yield return projects;
        }
    }
}
