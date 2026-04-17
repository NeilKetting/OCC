using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using OCC.Client.ViewModels.Core;
using OCC.Client.ViewModels.Messages;


namespace OCC.Client.Features.ProjectsHub.ViewModels
{
    public partial class ProjectsViewModel : ViewModelBase, 
        CommunityToolkit.Mvvm.Messaging.IRecipient<ProjectSelectedMessage>,
        CommunityToolkit.Mvvm.Messaging.IRecipient<SwitchTabMessage>,
        CommunityToolkit.Mvvm.Messaging.IRecipient<ProjectReportRequestMessage>
    {
        private readonly ProjectDetailViewModel _projectDetailVM;
        private readonly ProjectListViewModel _projectListVM;

        private readonly ProjectReportViewModel _projectReportVM;

        [ObservableProperty]
        private ProjectMainMenuViewModel _projectMainMenu;

        [ObservableProperty]
        private ViewModelBase _currentView;

        public ProjectsViewModel(
            ProjectMainMenuViewModel projectMenu, 
            ProjectListViewModel projectsListVM,
            ProjectDetailViewModel projectDetailVM,
            ProjectReportViewModel projectReportVM) 
        {
            ProjectMainMenu = projectMenu;
            _projectListVM = projectsListVM;
            _projectDetailVM = projectDetailVM;
            _projectReportVM = projectReportVM;

            // Default
            CurrentView = _projectListVM;

            ProjectMainMenu.PropertyChanged += Menu_PropertyChanged;

            // Register for Project Selection
            CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.RegisterAll(this);
        }

        private void Menu_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ProjectMainMenuViewModel.ActiveTab))
            {
                UpdateView();
            }
        }

        private void UpdateView()
        {
            switch (ProjectMainMenu.ActiveTab)
            {
                case "Reports":
                    CurrentView = _projectReportVM; // Use the new Report View
                    break;
                case "Projects":
                default:
                    CurrentView = _projectListVM;
                    break;
            }
        }

        public void Receive(ProjectSelectedMessage message)
        {
             // Switch to Detail View
             CurrentView = _projectDetailVM;
             _projectDetailVM.LoadTasks(message.Value.Id);
        }

        public void Receive(SwitchTabMessage message)
        {
            if (message.Value == "Projects")
            {
                // Force reset to List View (Side Menu "Home" behavior)
                CurrentView = _projectListVM;

                // Ensure tab is synchronized if needed
                if (ProjectMainMenu.ActiveTab != "Projects")
                {
                    ProjectMainMenu.ActiveTab = "Projects";
                }
            }
        }

        public void Receive(ProjectReportRequestMessage message)
        {
            CurrentView = _projectReportVM;
            _ = _projectReportVM.LoadReportAsync(message.Value);
            
            if (ProjectMainMenu.ActiveTab != "Reports")
            {
                ProjectMainMenu.ActiveTab = "Reports";
            }
        }
    }
}




