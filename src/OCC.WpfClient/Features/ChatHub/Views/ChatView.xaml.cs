using System;
using System.Linq;
using System.Windows.Controls;
using OCC.Shared.DTOs;
using OCC.WpfClient.Features.ChatHub.ViewModels;
using OCC.WpfClient.Features.ChatHub.Models;
using System.Diagnostics;

namespace OCC.WpfClient.Features.ChatHub.Views
{
    public partial class ChatView : UserControl
    {
        private ChatSessionModel? _currentSession;

        public ChatView()
        {
            InitializeComponent();
            DataContextChanged += ChatView_DataContextChanged;
        }

        private void ChatView_DataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is ChatViewModel oldVm)
            {
                oldVm.PropertyChanged -= OnViewModelPropertyChanged;
            }
            if (e.NewValue is ChatViewModel newVm)
            {
                newVm.PropertyChanged += OnViewModelPropertyChanged;
                newVm.RequestClearInput += OnRequestClearInput;
                UpdateMessageSubscription(null, newVm.SelectedSession);
            }
        }

        private void OnRequestClearInput(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() => {
                MessageTextBox.Text = string.Empty;
                Debug.WriteLine("[ChatView] Explicitly cleared MessageTextBox via event.");
            });
        }

        private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ChatViewModel.SelectedSession) && DataContext is ChatViewModel vm)
            {
                UpdateMessageSubscription(_currentSession, vm.SelectedSession);
                ScrollToBottom();
            }
        }

        private void UpdateMessageSubscription(ChatSessionModel? oldSession, ChatSessionModel? newSession)
        {
            if (oldSession != null) oldSession.Messages.CollectionChanged -= OnMessagesCollectionChanged;
            if (newSession != null) newSession.Messages.CollectionChanged += OnMessagesCollectionChanged;
            _currentSession = newSession;
        }

        private void OnMessagesCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
            {
                ScrollToBottom();
            }
        }

        private void ScrollToBottom()
        {
            // Give layout a moment to update
            Dispatcher.InvokeAsync(() => {
                MessagesScrollViewer?.ScrollToEnd();
            }, System.Windows.Threading.DispatcherPriority.Background);
        }

        private void MenuButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is Button btn && btn.ContextMenu != null)
            {
                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                btn.ContextMenu.IsOpen = true;
            }
        }

        private void Contact_Checked(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.DataContext is ChatUserDto contact && DataContext is ChatViewModel vm)
            {
                vm.ToggleContactSelectionCommand.Execute(contact);
            }
        }

        private void Contact_Unchecked(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.DataContext is ChatUserDto contact && DataContext is ChatViewModel vm)
            {
                vm.ToggleContactSelectionCommand.Execute(contact);
            }
        }
    }
}
