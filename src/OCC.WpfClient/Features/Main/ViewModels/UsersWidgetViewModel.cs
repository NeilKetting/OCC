using CommunityToolkit.Mvvm.ComponentModel;
using OCC.WpfClient.Services.Interfaces;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.WpfClient.Features.Main.ViewModels
{
    public partial class UsersWidgetViewModel : WidgetViewModelBase
    {
        private readonly IUserService _userService;

        [ObservableProperty]
        private int _userCount;

        public UsersWidgetViewModel(IUserService userService)
        {
            _userService = userService;
            WidgetId = "Users";
            Title = "Users Summary";
        }

        public override async Task RefreshDataAsync()
        {
            try
            {
                var users = await _userService.GetUsersAsync();
                UserCount = users.Count();
            }
            catch { }
        }
    }
}
