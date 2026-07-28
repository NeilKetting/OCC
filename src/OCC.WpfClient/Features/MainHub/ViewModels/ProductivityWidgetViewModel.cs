using CommunityToolkit.Mvvm.ComponentModel;
using OCC.WpfClient.Services.Interfaces;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.WpfClient.Features.MainHub.ViewModels
{
    public partial class ProductivityWidgetViewModel : WidgetViewModelBase
    {
        private readonly IProjectTaskService _taskService;

        [ObservableProperty]
        private int _completionRate;

        [ObservableProperty]
        private string _completionRateText = string.Empty;

        public ProductivityWidgetViewModel(IProjectTaskService taskService)
        {
            _taskService = taskService;
            WidgetId = "Productivity";
            Title = "Productivity Summary";
        }

        public override async Task RefreshDataAsync()
        {
            try
            {
                var tasks = await _taskService.GetTasksAsync(assignedToMe: true);
                var taskList = tasks.ToList();
                var totalTasksCount = taskList.Count;
                var completedTasksCount = taskList.Count(t => t.IsComplete);
                CompletionRate = totalTasksCount > 0 ? (int)Math.Round((double)completedTasksCount / totalTasksCount * 100) : 100;
                CompletionRateText = $"{completedTasksCount} of {totalTasksCount} tasks done";
            }
            catch { }
        }
    }
}
