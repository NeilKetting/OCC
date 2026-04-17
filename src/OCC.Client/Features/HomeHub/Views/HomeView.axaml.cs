using Avalonia.Controls;
using Avalonia.Input;
using OCC.Client.Features.HomeHub.ViewModels;

namespace OCC.Client.Features.HomeHub.Views
{
    public partial class HomeView : UserControl
    {
        public HomeView()
        {
            InitializeComponent();
        }

        private void OnOverlayPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.Source is Grid grid && grid.Name == "OverlayGrid")
            {
                if (DataContext is HomeViewModel vm)
                {
                    vm.CloseTaskDetailCommand.Execute(null);
                }
            }
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (DataContext is HomeViewModel vm && vm.IsTaskDetailVisible)
                {
                    vm.CloseTaskDetailCommand.Execute(null);
                    e.Handled = true;
                    return;
                }
            }
            base.OnKeyDown(e);
        }

        protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            var taskDetail = this.FindControl<Features.TaskHub.Views.Widgets.TaskDetailView>("TaskDetailView");
            if (taskDetail != null)
            {
                taskDetail.PropertyChanged += async (s, args) =>
                {
                    if (args.Property.Name == nameof(Avalonia.Visual.IsEffectivelyVisible) && args.NewValue is true)
                    {
                        await System.Threading.Tasks.Task.Delay(50);
                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => taskDetail.FocusInput(), Avalonia.Threading.DispatcherPriority.Background);
                    }
                };
            }
        }
    }
}
