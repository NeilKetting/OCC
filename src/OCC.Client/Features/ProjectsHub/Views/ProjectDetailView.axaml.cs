using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System;
using OCC.Shared.Models;
using OCC.Client.Features.ProjectsHub.ViewModels;


namespace OCC.Client.Features.ProjectsHub.Views;

public partial class ProjectDetailView : UserControl
{
    public ProjectDetailView()
    {
        InitializeComponent();
    }

    private void TaskGrid_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is DataGrid grid && 
            grid.SelectedItem is ProjectTask task && 
            DataContext is ProjectDetailViewModel vm)
        {
            // Trigger Pin
            if (vm.PinTaskDetailCommand.CanExecute(task))
            {
                vm.PinTaskDetailCommand.Execute(task);
            }
            e.Handled = true;
        }
    }

    private void OnOverlayPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is Grid grid && grid.Name == "OverlayGrid")
        {
            if (DataContext is ProjectDetailViewModel vm)
            {
                vm.CloseTaskDetailCommand.Execute(null);
            }
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (DataContext is ProjectDetailViewModel vm && vm.IsTaskDetailOpen)
            {
                vm.CloseTaskDetailCommand.Execute(null);
                e.Handled = true;
                return;
            }
        }
        base.OnKeyDown(e);
    }
}




