using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using System.Windows.Input;

namespace OCC.Client.Controls
{
    /// <summary>
    /// A custom control that combines AutoCompleteBox with a Dropdown toggle and "Add New" functionality.
    /// </summary>
    [TemplatePart("PART_ToggleButton", typeof(Button))]
    public class SearchableComboBox : AutoCompleteBox
    {
        private TextBox? _textBox;

        public static readonly StyledProperty<ICommand?> AddNewCommandProperty =
            AvaloniaProperty.Register<SearchableComboBox, ICommand?>(nameof(AddNewCommand));

        public static readonly StyledProperty<string> AddNewLabelProperty =
            AvaloniaProperty.Register<SearchableComboBox, string>(nameof(AddNewLabel), "Add new");

        public static readonly StyledProperty<bool> IsDropdownButtonVisibleProperty =
            AvaloniaProperty.Register<SearchableComboBox, bool>(nameof(IsDropdownButtonVisible), true);

        public static readonly StyledProperty<ICommand?> DropdownOpeningCommandProperty =
            AvaloniaProperty.Register<SearchableComboBox, ICommand?>(nameof(DropdownOpeningCommand));

        public static new readonly StyledProperty<string?> WatermarkProperty =
            AvaloniaProperty.Register<SearchableComboBox, string?>(nameof(Watermark));

        public ICommand? AddNewCommand
        {
            get => GetValue(AddNewCommandProperty);
            set => SetValue(AddNewCommandProperty, value);
        }

        public string AddNewLabel
        {
            get => GetValue(AddNewLabelProperty);
            set => SetValue(AddNewLabelProperty, value);
        }

        public bool IsDropdownButtonVisible
        {
            get => GetValue(IsDropdownButtonVisibleProperty);
            set => SetValue(IsDropdownButtonVisibleProperty, value);
        }

        public ICommand? DropdownOpeningCommand
        {
            get => GetValue(DropdownOpeningCommandProperty);
            set => SetValue(DropdownOpeningCommandProperty, value);
        }

        public new string? Watermark
        {
            get => GetValue(WatermarkProperty);
            set => SetValue(WatermarkProperty, value);
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            var toggleButton = e.NameScope.Find<Button>("PART_ToggleButton");
            if (toggleButton != null)
            {
                toggleButton.Click += ToggleButton_Click;
            }

            _textBox = e.NameScope.Find<TextBox>("PART_TextBox");
        }

        private void ToggleButton_Click(object? sender, RoutedEventArgs e)
        {
            if (IsDropDownOpen)
            {
                IsDropDownOpen = false;
            }
            else
            {
                // Execute command if set (e.g. to clear filters in VM)
                if (DropdownOpeningCommand?.CanExecute(null) == true)
                {
                    DropdownOpeningCommand.Execute(null);
                }

                _textBox?.Focus();
                
                // Use Post to ensure focus and command execution are processed
                Avalonia.Threading.Dispatcher.UIThread.Post(() => 
                {
                    PopulateComplete();
                    IsDropDownOpen = true;
                });
            }
        }

        protected override void OnKeyDown(Avalonia.Input.KeyEventArgs e)
        {
            base.OnKeyDown(e);
            
            // Allow opening dropdown with Alt+Down or similar if we want to mimic ComboBox precisely
            if (e.Key == Avalonia.Input.Key.Down && e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Alt))
            {
                IsDropDownOpen = !IsDropDownOpen;
                e.Handled = true;
            }
        }
    }
}
