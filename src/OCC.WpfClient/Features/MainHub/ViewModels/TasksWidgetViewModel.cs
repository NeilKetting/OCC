using CommunityToolkit.Mvvm.ComponentModel;
using OCC.WpfClient.Services.Interfaces;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.WpfClient.Features.MainHub.ViewModels
{
    public partial class TasksWidgetViewModel : WidgetViewModelBase
    {
        private readonly IProjectTaskService _taskService;

        [ObservableProperty]
        private int _taskCount;

        public TasksWidgetViewModel(IProjectTaskService taskService)
        {
            _taskService = taskService;
            WidgetId = "Tasks";
            Title = "Tasks Summary";
        }

        public override async Task RefreshDataAsync()
        {
            try
            {
                var tasks = await _taskService.GetTasksAsync(assignedToMe: true);
                TaskCount = tasks.Count(t => !t.IsComplete);
            }
            catch { }
        }
    }
}
