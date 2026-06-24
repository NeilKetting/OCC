using System;
using System.Windows;
using System.Windows.Controls;

namespace OCC.WpfClient.Features.EmployeeHub.Views
{
    public partial class EmployeeDetailView : UserControl
    {
        private int _currentColumns = -1;

        public EmployeeDetailView()
        {
            InitializeComponent();
            this.SizeChanged += EmployeeDetailView_SizeChanged;
        }

        private void EmployeeDetailView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            double width = e.NewSize.Width;
            int columns = 1;
            if (width >= 1000)
            {
                columns = 3;
            }
            else if (width >= 650)
            {
                columns = 2;
            }

            if (columns != _currentColumns)
            {
                _currentColumns = columns;
                RearrangeLayout(columns);
            }
        }

        private void RearrangeLayout(int columns)
        {
            RearrangeGrid(PersonalInfoGrid, columns);
            RearrangeGrid(EmploymentGrid, columns);
            RearrangeGrid(EmergencyGrid, columns);
            RearrangeGrid(BankingLeaveGrid, columns);
        }

        private void RearrangeGrid(Grid grid, int columns)
        {
            if (grid == null) return;

            // Clear existing row and column definitions
            grid.ColumnDefinitions.Clear();
            grid.RowDefinitions.Clear();

            // Add column definitions
            for (int i = 0; i < columns; i++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            // Get children
            var children = grid.Children;
            int childCount = children.Count;
            if (childCount == 0) return;

            // Define row and column for each child
            int currentColumn = 0;
            int currentRow = 0;

            // Add first row definition
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            for (int i = 0; i < childCount; i++)
            {
                if (children[i] is not FrameworkElement child) continue;

                // Check if this child should span multiple columns
                int colSpan = 1;
                if (child.Tag is string tag && tag == "FullWidth")
                {
                    colSpan = columns;
                }

                // If we need to move to the next row because we don't fit in the remaining columns of this row
                if (currentColumn + colSpan > columns && currentColumn > 0)
                {
                    currentColumn = 0;
                    currentRow++;
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                }

                Grid.SetColumn(child, currentColumn);
                Grid.SetRow(child, currentRow);
                Grid.SetColumnSpan(child, colSpan);

                // Margin adjustment for layout separation (Right margin only if not last column)
                Thickness margin = child.Margin;
                child.Margin = new Thickness(
                    0, 
                    margin.Top, 
                    (currentColumn + colSpan < columns) ? 15 : 0, 
                    margin.Bottom
                );

                currentColumn += colSpan;
                if (currentColumn >= columns)
                {
                    currentColumn = 0;
                    if (i < childCount - 1)
                    {
                        currentRow++;
                        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    }
                }
            }
        }
    }
}
