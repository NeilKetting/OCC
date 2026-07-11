using CommunityToolkit.Mvvm.ComponentModel;
using OCC.WpfClient.Services.Interfaces;
using OCC.Shared.Models;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.WpfClient.Features.Main.ViewModels
{
    public partial class TodosWidgetViewModel : WidgetViewModelBase
    {
        private readonly IProjectTaskService _taskService;

        [ObservableProperty]
        private int _todoCount;

        public TodosWidgetViewModel(IProjectTaskService taskService)
        {
            _taskService = taskService;
            WidgetId = "Todos";
            Title = "To-Dos Summary";
        }

        public override async Task RefreshDataAsync()
        {
            try
            {
                var tasks = await _taskService.GetTasksAsync(assignedToMe: true);
                TodoCount = tasks.Count(t => t.Type == TaskType.PersonalToDo && !t.IsComplete);
            }
            catch { }
        }
    }
}
