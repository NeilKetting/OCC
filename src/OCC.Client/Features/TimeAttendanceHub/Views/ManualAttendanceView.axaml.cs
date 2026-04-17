using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using OCC.Client.Features.TimeAttendanceHub.ViewModels;
using System.Linq;

namespace OCC.Client.Features.TimeAttendanceHub.Views
{
    public partial class ManualAttendanceView : UserControl
    {
        public ManualAttendanceView()
        {
            InitializeComponent();
        }

        private void EmployeesDataGrid_KeyDown(object? sender, KeyEventArgs e)
        {
            if (sender is DataGrid grid && e.Key >= Key.A && e.Key <= Key.Z)
            {
                var searchChar = e.Key.ToString();
                if (DataContext is ManualAttendanceViewModel vm)
                {
                    var matchingItems = vm.Employees.Where(emp =>
                        !string.IsNullOrEmpty(emp.DisplayName) &&
                        emp.DisplayName.StartsWith(searchChar, System.StringComparison.OrdinalIgnoreCase)).ToList();

                    if (matchingItems.Any() && grid.SelectedItem is SelectableEmployeeViewModel currentItem)
                    {
                        var currentIndex = matchingItems.IndexOf(currentItem);
                        var nextIndex = (currentIndex + 1) % matchingItems.Count;
                        var nextItem = matchingItems[nextIndex];

                        grid.SelectedItem = nextItem;
                        grid.ScrollIntoView(nextItem, null);
                        e.Handled = true;
                    }
                }
            }
        }
    }
}
