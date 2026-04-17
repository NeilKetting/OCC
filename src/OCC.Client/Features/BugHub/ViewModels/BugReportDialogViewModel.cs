using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Client.Services.Interfaces;
using OCC.Client.ViewModels.Core; // Added
using OCC.Shared.Models;
using System;
using System.Threading.Tasks;

namespace OCC.Client.Features.BugHub.ViewModels
{
    public partial class BugReportDialogViewModel : ViewModelBase
    {
        private readonly IBugReportService _bugService;
        private readonly IAuthService _authService;
        private readonly Action _closeAction;

        [ObservableProperty]
        private string _viewName;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
        private string _description = string.Empty;

        [ObservableProperty]
        private bool _isSubmitting;

        [ObservableProperty]
        private string? _screenshotBase64;

        [ObservableProperty]
        private BugReportType _selectedType = BugReportType.Bug;

        public BugReportType[] AvailableTypes { get; } = (BugReportType[])Enum.GetValues(typeof(BugReportType));

        public BugReportDialogViewModel(
            IBugReportService bugService, 
            IAuthService authService,
            string viewName, 
            Action closeAction)
        {
            _bugService = bugService;
            _authService = authService;
            _viewName = viewName;
            _closeAction = closeAction;
        }

        private bool CanSubmit() => !string.IsNullOrWhiteSpace(Description) && !IsSubmitting;

        [RelayCommand(CanExecute = nameof(CanSubmit))]
        private async Task SubmitAsync()
        {
            if (IsSubmitting) return;

            try
            {
                IsSubmitting = true;
                
                var currentUser = _authService.CurrentUser;
                
                var report = new BugReport
                {
                    Id = Guid.NewGuid(),
                    ReporterId = currentUser?.Id,
                    ReporterName = currentUser?.FirstName + " " + currentUser?.LastName,
                    ReportedDate = DateTime.UtcNow,
                    ViewName = ViewName,
                    Description = Description,
                    Type = SelectedType,
                    Status = "Open",
                    ScreenshotBase64 = ScreenshotBase64
                };

                await _bugService.SubmitBugAsync(report);
                
                _closeAction?.Invoke();
            }
            catch (Exception ex)
            {
                // In a real app, maybe show error in dialog. 
                // For now, minimal handling or logging if possible.
                System.Diagnostics.Debug.WriteLine($"Error submitting bug: {ex.Message}");
            }
            finally
            {
                IsSubmitting = false;
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            _closeAction?.Invoke();
        }
    }
}
