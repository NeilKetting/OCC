using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using OCC.Client.Features.ProjectsHub.ViewModels;

namespace OCC.Client.Features.ProjectsHub.Views;

    public partial class ProjectListView : UserControl
    {
        public ProjectListView()
        {
            InitializeComponent();
        }

        private void DataGrid_DoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
        {
            if (sender is DataGrid dg && dg.SelectedItem is ViewModels.ProjectDashboardItemViewModel project && 
                DataContext is ViewModels.ProjectListViewModel vm)
            {
                vm.OpenProjectCommand.Execute(project);
            }
        }
    }




