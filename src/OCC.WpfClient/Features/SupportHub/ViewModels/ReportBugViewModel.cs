using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Shared.Models;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using CommunityToolkit.Mvvm.Messaging;
using OCC.WpfClient.Infrastructure.Messages;

namespace OCC.WpfClient.Features.SupportHub.ViewModels
{
    public partial class ReportBugViewModel : ViewModelBase
    {
        private readonly IBugReportService _bugService;
        private readonly IAuthService _authService;

        [ObservableProperty]
        private string _viewName = "Main Shell";

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
        private string _description = string.Empty;

        [ObservableProperty]
        private BugReportType _selectedType = BugReportType.Bug;

        [ObservableProperty]
        private string? _screenshotBase64;

        [ObservableProperty]
        private bool _hasScreenshot;

        public BugReportType[] AvailableTypes { get; } = (BugReportType[])Enum.GetValues(typeof(BugReportType));

        public ReportBugViewModel(IBugReportService bugService, IAuthService authService)
        {
            _bugService = bugService;
            _authService = authService;
            Title = "Report Bug";
        }

        public void Initialize(string currentView)
        {
            ViewName = currentView;
            
            // Capture screenshot of the main window
            var mainWindow = System.Windows.Application.Current.MainWindow;
            if (mainWindow != null)
            {
                ScreenshotBase64 = ScreenshotHelper.CaptureWindowToBase64(mainWindow);
                HasScreenshot = !string.IsNullOrEmpty(ScreenshotBase64);
            }
        }

        private bool CanSubmit() => !string.IsNullOrWhiteSpace(Description) && !IsBusy;

        [RelayCommand(CanExecute = nameof(CanSubmit))]
        private async Task SubmitAsync()
        {
            if (IsBusy) return;

            try
            {
                IsBusy = true;
                BusyText = "Submitting report...";

                var currentUser = _authService.CurrentUser;
                var report = new BugReport
                {
                    Id = Guid.NewGuid(),
                    ReporterId = currentUser?.Id,
                    ReporterName = $"{currentUser?.FirstName} {currentUser?.LastName}".Trim(),
                    ReportedDate = DateTime.UtcNow,
                    ViewName = ViewName,
                    Description = Description,
                    Type = SelectedType,
                    Status = "Open",
                    ScreenshotBase64 = ScreenshotBase64
                };

                await _bugService.SubmitBugAsync(report);
                
                // Clear form
                Description = string.Empty;
                ScreenshotBase64 = null;
                HasScreenshot = false;
                
                BusyText = "Success! Report submitted.";
                await Task.Delay(2000);
            }
            catch (Exception ex)
            {
                BusyText = "Error submitting report.";
                System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
                await Task.Delay(3000);
            }
            finally
            {
                IsBusy = false;
                BusyText = string.Empty;
            }
        }

        [RelayCommand]
        private void ClearScreenshot()
        {
            ScreenshotBase64 = null;
            HasScreenshot = false;
        }

        [RelayCommand]
        private void CloseHub()
        {
            WeakReferenceMessenger.Default.Send(new CloseHubMessage(this));
        }

        [RelayCommand]
        private void ViewScreenshot()
        {
            // Logic to show the screenshot in a bigger view or save it
        }
    }
}
