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
            services.AddTransient<ProjectsViewModel>();
            services.AddTransient<ProjectDetailViewModel>();
            services.AddTransient<ProjectSpecificDashboardViewModel>();
            services.AddTransient<ProjectTasksViewModel>();
            services.AddTransient<TaskDetailViewModel>();
            services.AddTransient<CreateProjectViewModel>();
            services.AddTransient<ProjectGanttViewModel>();
        }

        public void RegisterRoutes(INavigationService navigationService)
        {
            navigationService.RegisterRoute(NavigationRoutes.ProjectDashboard, typeof(ProjectDashboardViewModel));
            navigationService.RegisterRoute(NavigationRoutes.Projects, typeof(ProjectsViewModel));
            navigationService.RegisterRoute(NavigationRoutes.ProjectDetail, typeof(ProjectDetailViewModel));
        }

        public IEnumerable<NavItem> GetNavigationItems()
        {
            var projects = new NavItem("Projects", "IconPortfolio", string.Empty, "Operations");

            projects.Children.Add(new NavItem(
                "Project Dashboard",
                "IconSummary",
                NavigationRoutes.ProjectDashboard,
                "Operations"));

            projects.Children.Add(new NavItem(
                "Projects",
                "IconList",
                NavigationRoutes.Projects,
                "Operations"));

            yield return projects;
        }
    }
}
