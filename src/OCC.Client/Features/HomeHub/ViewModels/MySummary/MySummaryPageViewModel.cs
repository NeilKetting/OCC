using CommunityToolkit.Mvvm.ComponentModel;
using OCC.Client.ViewModels.Core;
using OCC.Client.Features.HomeHub.ViewModels.Dashboard;

namespace OCC.Client.Features.HomeHub.ViewModels.MySummary
{
    public partial class MySummaryPageViewModel : ViewModelBase
    {
        [ObservableProperty]
        private SummaryViewModel _summary;

        [ObservableProperty]
        private TasksWidgetViewModel _tasksWidget;

        public MySummaryPageViewModel(SummaryViewModel summary, TasksWidgetViewModel tasksWidget)
        {
            Summary = summary;
            TasksWidget = tasksWidget;
        }
    }
}
