using Avalonia.Controls;
using OCC.Client.Features.HseqHub.ViewModels;

namespace OCC.Client.Features.HseqHub.Views
{
    public partial class TrainingView : UserControl
    {
        public TrainingView()
        {
            InitializeComponent();
        }

        private void DataGrid_DoubleTapped(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (sender is DataGrid dg && dg.SelectedItem is TrainingRecordViewModel vm)
            {
                if (DataContext is TrainingViewModel mainVm)
                {
                    mainVm.EditRecordCommand.Execute(vm);
                }
            }
        }
    }
}
