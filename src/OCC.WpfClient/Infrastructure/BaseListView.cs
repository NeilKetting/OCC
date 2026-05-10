using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using OCC.WpfClient.Infrastructure;

namespace OCC.WpfClient.Infrastructure
{
    public class BaseListView : UserControl
    {
        public static readonly DependencyProperty IsDrawerOpenProperty =
            DependencyProperty.Register("IsDrawerOpen", typeof(bool), typeof(BaseListView), 
                new PropertyMetadata(false, OnIsDrawerOpenChanged));

        public bool IsDrawerOpen
        {
            get => (bool)GetValue(IsDrawerOpenProperty);
            set => SetValue(IsDrawerOpenProperty, value);
        }

        public static readonly DependencyProperty DrawerWidthProperty =
            DependencyProperty.Register("DrawerWidth", typeof(double), typeof(BaseListView), 
                new PropertyMetadata(550.0));

        public double DrawerWidth
        {
            get => (double)GetValue(DrawerWidthProperty);
            set => SetValue(DrawerWidthProperty, value);
        }

        public static readonly DependencyProperty OverlayContentProperty =
            DependencyProperty.Register("OverlayContent", typeof(object), typeof(BaseListView), 
                new PropertyMetadata(null));

        public object OverlayContent
        {
            get => GetValue(OverlayContentProperty);
            set => SetValue(OverlayContentProperty, value);
        }

        public BaseListView()
        {
            this.Loaded += BaseListView_Loaded;
        }

        private void BaseListView_Loaded(object sender, RoutedEventArgs e)
        {
            var dataGrid = FindVisualChild<DataGrid>(this);
            if (dataGrid != null)
            {
                dataGrid.ColumnReordered += DataGrid_ColumnReordered;
            }

            if (_drawerOverlay == null)
            {
                InjectDrawerOverlay();
            }
        }

        private static void OnIsDrawerOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is BaseListView view)
            {
                view.HandleDrawerTransition((bool)e.NewValue);
            }
        }

        private Grid? _drawerOverlay;
        private Storyboard? _openStoryboard;
        private Storyboard? _closeStoryboard;

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            _drawerOverlay = this.FindName("DrawerOverlay") as Grid;

            if (_drawerOverlay != null)
            {
                _openStoryboard = _drawerOverlay.Resources["OpenDrawer"] as Storyboard ?? this.TryFindResource("OpenDrawer") as Storyboard;
                _closeStoryboard = _drawerOverlay.Resources["CloseDrawer"] as Storyboard ?? this.TryFindResource("CloseDrawer") as Storyboard;
            }
        }

        private void HandleDrawerTransition(bool isOpen)
        {
            // If template isn't applied yet, we might not have the overlay. 
            // In WPF, DependencyProperties can change before OnApplyTemplate.
            // If it happens, we apply template forcefully.
            if (_drawerOverlay == null)
            {
                InjectDrawerOverlay();
            }

            if (_drawerOverlay == null) return;

            if (isOpen)
            {
                _drawerOverlay.Visibility = Visibility.Visible;
                if (_openStoryboard != null)
                {
                    _openStoryboard.Begin(_drawerOverlay); // Begin on the Grid so it can resolve TargetName
                }
            }
            else
            {
                if (_closeStoryboard != null)
                {
                    var sb = _closeStoryboard.Clone();
                    sb.Completed += (s, args) =>
                    {
                        if (!IsDrawerOpen)
                            _drawerOverlay.Visibility = Visibility.Collapsed;
                    };
                    sb.Begin(_drawerOverlay);
                }
                else
                {
                    _drawerOverlay.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void InjectDrawerOverlay()
        {
            if (this.Content is not Panel rootPanel) return;

            // Check if already injected or manually defined
            if (rootPanel.FindName("DrawerOverlay") is Grid existingDrawer)
            {
                _drawerOverlay = existingDrawer;
                _openStoryboard = _drawerOverlay.Resources["OpenDrawer"] as Storyboard ?? this.TryFindResource("OpenDrawer") as Storyboard;
                _closeStoryboard = _drawerOverlay.Resources["CloseDrawer"] as Storyboard ?? this.TryFindResource("CloseDrawer") as Storyboard;
                return;
            }

            var grid = new Grid { Name = "DrawerOverlay", Visibility = Visibility.Collapsed };
            if (rootPanel is Grid rg && rg.RowDefinitions.Count > 0)
            {
                Grid.SetRowSpan(grid, Math.Max(1, rg.RowDefinitions.Count + 10));
            }
            
            var dimmer = new Border { Name = "BackgroundDimmer", Background = new SolidColorBrush(Color.FromArgb(0x80, 0, 0, 0)), Opacity = 0 };
            var mouseBinding = new MouseBinding { MouseAction = MouseAction.LeftClick };
            BindingOperations.SetBinding(mouseBinding, InputBinding.CommandProperty, new Binding("CloseOverlayCommand"));
            dimmer.InputBindings.Add(mouseBinding);
            grid.Children.Add(dimmer);
            
            var drawer = new Border 
            { 
                Name = "DrawerContent", 
                HorizontalAlignment = HorizontalAlignment.Right, 
                BorderThickness = new Thickness(1, 0, 0, 0)
            };
            drawer.SetBinding(FrameworkElement.WidthProperty, new Binding("DrawerWidth") { Source = this });
            drawer.SetResourceReference(Border.BackgroundProperty, "BackgroundDark");
            drawer.SetResourceReference(Border.BorderBrushProperty, "GlassBorder");
            drawer.RenderTransform = new TranslateTransform { X = DrawerWidth };
            
            var contentControl = new ContentControl();
            contentControl.SetBinding(ContentControl.ContentProperty, new Binding("OverlayContent") { Source = this });
            drawer.Child = contentControl;
            grid.Children.Add(drawer);
            
            rootPanel.Children.Add(grid);
            _drawerOverlay = grid;

            // Register names for Storyboard resolution
            if (rootPanel.FindName("DrawerContent") == null) rootPanel.RegisterName("DrawerContent", drawer);
            if (rootPanel.FindName("BackgroundDimmer") == null) rootPanel.RegisterName("BackgroundDimmer", dimmer);

            _openStoryboard = new Storyboard();
            var slideIn = new DoubleAnimation { To = 0, Duration = TimeSpan.FromSeconds(0.6), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            Storyboard.SetTargetName(slideIn, "DrawerContent");
            Storyboard.SetTargetProperty(slideIn, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));
            var fadeIn = new DoubleAnimation { To = 1, Duration = TimeSpan.FromSeconds(0.3) };
            Storyboard.SetTargetName(fadeIn, "BackgroundDimmer");
            Storyboard.SetTargetProperty(fadeIn, new PropertyPath("Opacity"));
            _openStoryboard.Children.Add(slideIn);
            _openStoryboard.Children.Add(fadeIn);

            _closeStoryboard = new Storyboard();
            var slideOut = new DoubleAnimation { Duration = TimeSpan.FromSeconds(0.4), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
            BindingOperations.SetBinding(slideOut, DoubleAnimation.ToProperty, new Binding("DrawerWidth") { Source = this });
            Storyboard.SetTargetName(slideOut, "DrawerContent");
            Storyboard.SetTargetProperty(slideOut, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));
            var fadeOut = new DoubleAnimation { To = 0, Duration = TimeSpan.FromSeconds(0.3) };
            Storyboard.SetTargetName(fadeOut, "BackgroundDimmer");
            Storyboard.SetTargetProperty(fadeOut, new PropertyPath("Opacity"));
            _closeStoryboard.Children.Add(slideOut);
            _closeStoryboard.Children.Add(fadeOut);

            // Important: Storyboards must be scoped to the rootPanel to resolve TargetNames
            NameScope.SetNameScope(_openStoryboard, NameScope.GetNameScope(rootPanel));
            NameScope.SetNameScope(_closeStoryboard, NameScope.GetNameScope(rootPanel));
        }


        private void DataGrid_ColumnReordered(object? sender, DataGridColumnEventArgs e)
        {
            if (DataContext != null)
            {
                try
                {
                    dynamic vm = DataContext;
                    vm.SaveLayoutCommand?.Execute(null);
                }
                catch { }
            }
        }

        protected T? FindVisualChild<T>(DependencyObject? obj, Func<T, bool>? filter = null) where T : DependencyObject
        {
            if (obj == null) return null;
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(obj, i);
                if (child != null && child is T tChild)
                {
                    if (filter == null || filter(tChild))
                        return tChild;
                }
                
                T? childOfChild = FindVisualChild<T>(child, filter);
                if (childOfChild != null)
                    return childOfChild;
            }
            return null;
        }
    }
}
