using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Client.ViewModels;
using System.Collections.ObjectModel;

using OCC.Client.ViewModels.Core;

namespace OCC.Client.Features.TaskHub.ViewModels
{
    public partial class ProjectGroupViewModel : ViewModelBase
    {
        [ObservableProperty]
        private string _projectName = string.Empty;

        [ObservableProperty]
        private bool _isExpanded = true;

        public ObservableCollection<TaskTreeItemViewModel> RootTasks { get; } = new();

        public ProjectGroupViewModel(string projectName)
        {
            ProjectName = projectName;
        }

        [RelayCommand]
        private void ToggleExpand()
        {
            IsExpanded = !IsExpanded;
        }
    }
}




