using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OCC.Shared.Models;
using OCC.WpfClient.Infrastructure.Messages;
using OCC.WpfClient.Services.Interfaces;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.WpfClient.Features.MainHub.ViewModels
{
    public partial class NoticeBoardWidgetViewModel : WidgetViewModelBase
    {
        private readonly INoticeBoardService _noticeBoardService;
        private readonly IAuthService _authService;
        private readonly IToastService _toastService;

        [ObservableProperty]
        private ObservableCollection<NoticeBoardItem> _notices = new();

        [ObservableProperty]
        private bool _canManageNotices;

        [ObservableProperty]
        private bool _isAddingNotice;

        [ObservableProperty]
        private string _newTitle = string.Empty;

        [ObservableProperty]
        private string _newContent = string.Empty;

        [ObservableProperty]
        private NoticeCategory _newCategory = NoticeCategory.Announcement;

        public NoticeBoardWidgetViewModel(INoticeBoardService noticeBoardService, IAuthService authService, IToastService toastService)
        {
            _noticeBoardService = noticeBoardService;
            _authService = authService;
            _toastService = toastService;

            WidgetId = "NoticeBoard";
            Title = "Notice Board";

            // Check permissions (Admin/Office can add/delete notices)
            var role = _authService.CurrentUser?.UserRole;
            CanManageNotices = role == UserRole.Admin || role == UserRole.Office;

            // Listen for SignalR real-time updates
            WeakReferenceMessenger.Default.Register<NoticeBoardUpdatedMessage>(this, async (r, m) =>
            {
                await RefreshDataAsync();
            });
        }

        public override async Task RefreshDataAsync()
        {
            try
            {
                var activeNotices = await _noticeBoardService.GetActiveNoticesAsync();
                App.Current.Dispatcher.Invoke(() =>
                {
                    Notices.Clear();
                    foreach (var notice in activeNotices)
                    {
                        Notices.Add(notice);
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to refresh notices: {ex.Message}");
            }
        }

        [RelayCommand]
        private void ToggleAddNotice()
        {
            IsAddingNotice = !IsAddingNotice;
            if (IsAddingNotice)
            {
                NewTitle = string.Empty;
                NewContent = string.Empty;
                NewCategory = NoticeCategory.Announcement;
            }
        }

        [RelayCommand]
        private async Task PostNotice()
        {
            if (string.IsNullOrWhiteSpace(NewTitle) || string.IsNullOrWhiteSpace(NewContent))
            {
                _toastService.ShowWarning("Validation", "Title and content are required.");
                return;
            }

            try
            {
                var item = new NoticeBoardItem
                {
                    Title = NewTitle.Trim(),
                    Content = NewContent.Trim(),
                    Category = NewCategory,
                    IsPinned = false
                };

                await _noticeBoardService.CreateNoticeAsync(item);
                
                _toastService.ShowSuccess("Success", "Notice posted successfully.");
                IsAddingNotice = false;
            }
            catch (Exception ex)
            {
                _toastService.ShowError("Error", $"Failed to post notice: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task DeleteNotice(Guid id)
        {
            try
            {
                bool success = await _noticeBoardService.DeleteNoticeAsync(id);
                if (success)
                {
                    _toastService.ShowSuccess("Success", "Notice removed.");
                }
            }
            catch (Exception ex)
            {
                _toastService.ShowError("Error", $"Failed to delete notice: {ex.Message}");
            }
        }
    }
}
