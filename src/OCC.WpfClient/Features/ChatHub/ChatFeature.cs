using Microsoft.Extensions.DependencyInjection;
using OCC.WpfClient.Features.ChatHub.ViewModels;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Services.Interfaces;

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
            yield return new NavItem("Chat", NavigationRoutes.Chat, "Main", iconColor: "#00FF00", iconCode: "\uE8BD");
        }
    }
}
