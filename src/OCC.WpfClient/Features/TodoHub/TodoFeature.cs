using Microsoft.Extensions.DependencyInjection;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using OCC.WpfClient.Features.TodoHub.ViewModels;
using System.Collections.Generic;

namespace OCC.WpfClient.Features.TodoHub
{
    public class TodoFeature : IFeature
    {
        public string Name => "To-Dos";
        public int Order => 26; // Placed after Calendar (25)

        public void RegisterServices(IServiceCollection services)
        {
            services.AddTransient<TodoHubViewModel>();
        }

        public void RegisterRoutes(INavigationService navigationService)
        {
            navigationService.RegisterRoute("Todo.TodoHub", typeof(TodoHubViewModel));
        }

        public IEnumerable<NavItem> GetNavigationItems()
        {
            yield return new NavItem(
                label: "To-Dos",
                route: "Todo.TodoHub",
                category: "Workspace",
                iconColor: "#FB923C",
                iconCode: "\uE10F"
            );
        }
    }
}
