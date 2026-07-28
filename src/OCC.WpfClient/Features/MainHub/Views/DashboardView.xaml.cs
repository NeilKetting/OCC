using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using OCC.WpfClient.Features.MainHub.ViewModels;

namespace OCC.WpfClient.Features.MainHub.Views
{
    public partial class DashboardView : UserControl
    {
        private Point _clickOffset;
        private Point _mouseDragStartPoint;
        private WidgetViewModelBase? _draggedWidget;
        private Border? _draggedBorder;
        private double _accumulatedX;
        private double _accumulatedY;
        private bool _isDragging;

        public DashboardView()
        {
            InitializeComponent();
        }

        private void OnWidgetMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;

            if (e.OriginalSource is DependencyObject depObj)
            {
                DependencyObject current = depObj;
                while (current != null && current != sender as DependencyObject)
                {
                    if (current is ButtonBase || current is Thumb)
                    {
                        return; // Let the button or thumb handle it!
                    }
                    current = VisualTreeHelper.GetParent(current);
                }
            }

            var border = sender as Border;
            if (border == null) return;

            _draggedWidget = border.DataContext as WidgetViewModelBase;
            if (_draggedWidget == null) return;

            _draggedBorder = border;
            _clickOffset = e.GetPosition(border);
            _mouseDragStartPoint = e.GetPosition(itemsControl);
            _isDragging = true;
            border.CaptureMouse();
        }

        private void OnWidgetMouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging && _draggedWidget != null && _draggedBorder != null)
            {
                var currentPoint = e.GetPosition(itemsControl);
                double deltaX = currentPoint.X - _mouseDragStartPoint.X;
                double deltaY = currentPoint.Y - _mouseDragStartPoint.Y;

                var transform = _draggedBorder.RenderTransform as TranslateTransform;
                if (transform == null)
                {
                    transform = new TranslateTransform();
                    _draggedBorder.RenderTransform = transform;
                }
                transform.X = deltaX;
                transform.Y = deltaY;
            }
        }

        private void OnWidgetMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                if (_draggedBorder != null)
                {
                    _draggedBorder.ReleaseMouseCapture();

                    var currentPoint = e.GetPosition(itemsControl);

                    var transform = _draggedBorder.RenderTransform as TranslateTransform;
                    if (transform != null)
                    {
                        transform.X = 0;
                        transform.Y = 0;
                    }

                    double totalWidth = itemsControl.ActualWidth;
                    double columnWidth = totalWidth / 4;
                    double rowHeight = 110;

                    double widgetLeft = currentPoint.X - _clickOffset.X;
                    double widgetTop = currentPoint.Y - _clickOffset.Y;

                    int newCol = (int)Math.Round(widgetLeft / columnWidth);
                    int newRow = (int)Math.Round(widgetTop / rowHeight);

                    if (_draggedWidget != null)
                    {
                        newCol = Math.Max(0, Math.Min(4 - _draggedWidget.ColumnSpan, newCol));
                        newRow = Math.Max(0, newRow);

                        _draggedWidget.Column = newCol;
                        _draggedWidget.Row = newRow;

                        if (DataContext is DashboardViewModel mainVm)
                        {
                            mainVm.ResolveOverlaps(_draggedWidget);
                        }
                    }
                }
                _draggedWidget = null;
                _draggedBorder = null;
            }
        }

        private void OnResizeThumbDragDelta(object sender, DragDeltaEventArgs e)
        {
            var thumb = sender as Thumb;
            if (thumb == null) return;

            var widget = thumb.DataContext as WidgetViewModelBase;
            if (widget == null) return;

            _accumulatedX += e.HorizontalChange;
            _accumulatedY += e.VerticalChange;

            double totalWidth = itemsControl.ActualWidth;
            double columnWidth = totalWidth / 4;
            double rowHeight = 110;

            if (_accumulatedX > columnWidth * 0.5)
            {
                if (widget.Column + widget.ColumnSpan < 4)
                {
                    widget.ColumnSpan++;
                    _accumulatedX -= columnWidth;
                }
                else
                {
                    _accumulatedX = columnWidth * 0.5;
                }
            }
            else if (_accumulatedX < -columnWidth * 0.5)
            {
                if (widget.ColumnSpan > 1)
                {
                    widget.ColumnSpan--;
                    _accumulatedX += columnWidth;
                }
                else
                {
                    _accumulatedX = -columnWidth * 0.5;
                }
            }

            if (_accumulatedY > rowHeight * 0.5)
            {
                widget.RowSpan++;
                _accumulatedY -= rowHeight;
            }
            else if (_accumulatedY < -rowHeight * 0.5)
            {
                if (widget.RowSpan > 1)
                {
                    widget.RowSpan--;
                    _accumulatedY += rowHeight;
                }
                else
                {
                    _accumulatedY = -rowHeight * 0.5;
                }
            }
        }

        private void OnResizeThumbDragCompleted(object sender, DragCompletedEventArgs e)
        {
            _accumulatedX = 0;
            _accumulatedY = 0;

            var thumb = sender as Thumb;
            var widget = thumb?.DataContext as WidgetViewModelBase;
            if (widget != null)
            {
                NotifyLayoutChanged(widget);
            }
        }

        private void NotifyLayoutChanged(WidgetViewModelBase widget)
        {
            if (DataContext is DashboardViewModel mainVm)
            {
                mainVm.ResolveOverlaps(widget);
            }
        }
    }
}
