using Microsoft.Extensions.DependencyInjection;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using OCC.WpfClient.Features.ChatHub.ViewModels;
using System.Collections.Generic;

namespace OCC.WpfClient.Features.ChatHub
{
    public class ChatFeature : IFeature
    {
        public string Name => "Chat";
        public int Order => 10;

        public void RegisterServices(IServiceCollection services)
        {
            services.AddTransient<ChatViewModel>();
        }

        public void RegisterRoutes(INavigationService navigationService)
        {
            navigationService.RegisterRoute(NavigationRoutes.Chat, typeof(ChatViewModel));
        }

        public IEnumerable<NavItem> GetNavigationItems()
        {
            yield return new NavItem("Chat", "IconChat", NavigationRoutes.Chat, "Main");
        }
    }
}
