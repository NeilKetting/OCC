using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OCC.WpfClient.Services.Interfaces;
using OCC.WpfClient.Infrastructure.Messages;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.WpfClient.Features.MainHub.ViewModels
{
    public partial class TodosWidgetViewModel : WidgetViewModelBase
    {
        private readonly ITodoService _todoService;

        [ObservableProperty]
        private int _todoCount;

        public TodosWidgetViewModel(ITodoService todoService)
        {
            _todoService = todoService;
            WidgetId = "Todos";
            Title = "To-Dos Summary";
        }

        public override async Task RefreshDataAsync()
        {
            try
            {
                var list = await _todoService.GetTodosAsync();
                TodoCount = list.Count(t => !t.IsComplete);
            }
            catch { }
        }

        [RelayCommand]
        private void OpenTodosHub()
        {
            WeakReferenceMessenger.Default.Send(new OpenHubMessage("Todo.TodoHub"));
        }
    }
}
