using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Managers.Interfaces;
using OCC.Client.Services.Repositories.Interfaces;
using OCC.Shared.Models;

namespace OCC.Client.Services
{
    public class DialogService : IDialogService
    {
        private readonly IServiceProvider _serviceProvider;

        public DialogService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task ShowBugReportAsync(string viewName, string? screenshotBase64 = null)
        {
             if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
             {
                 var dialog = new OCC.Client.Features.BugHub.Views.BugReportDialog();
                 var vm = new OCC.Client.Features.BugHub.ViewModels.BugReportDialogViewModel(
                     Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IBugReportService>(_serviceProvider),
                     Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IAuthService>(_serviceProvider),
                     viewName,
                     () => Avalonia.Threading.Dispatcher.UIThread.Post(() => dialog.Close())
                 );
                 vm.ScreenshotBase64 = screenshotBase64;
                 dialog.DataContext = vm;
                 await dialog.ShowDialog(desktop.MainWindow);
             }
        }

        public async Task<string?> PickFileAsync(string title, IEnumerable<string> extensions)
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
            {
                var topLevel = TopLevel.GetTopLevel(desktop.MainWindow);
                if (topLevel == null) return null;

                var patterns = extensions?.Select(e => e.StartsWith("*.") ? e : $"*.{e.TrimStart('.')}").ToList();

                var options = new FilePickerOpenOptions
                {
                    Title = title,
                    AllowMultiple = false,
                    FileTypeFilter = new List<FilePickerFileType>
                    {
                        new FilePickerFileType("Supported Files")
                        {
                             Patterns = patterns
                        },
                        FilePickerFileTypes.All
                    }
                };

                var files = await topLevel.StorageProvider.OpenFilePickerAsync(options);
                return files.FirstOrDefault()?.Path.LocalPath;
            }
            return null;
        }

        public async Task<bool> ShowConfirmationAsync(string title, string message)
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
            {
                var dialog = new OCC.Client.Views.ConfirmationDialog(title, message);
                await dialog.ShowDialog(desktop.MainWindow);
                return dialog.Result;
            }
            return false;
        }

        public async Task ShowAlertAsync(string title, string message)
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
            {
                // Re-use ConfirmationDialog but hide Cancel button? Or just use it as is for now.
                // Minimal implementation:
                var dialog = new OCC.Client.Views.ConfirmationDialog(title, message);
                // We'd ideally hide the Cancel button, but for MVP it's fine.
                await dialog.ShowDialog(desktop.MainWindow);
            }
        }
        public async Task<(bool Confirmed, string? Reason, string? Note, string? FilePath)> ShowLeaveEarlyReasonAsync()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
            {
                var dialog = new OCC.Client.Features.TimeAttendanceHub.Views.LeaveEarlyReasonDialog();
                var result = await dialog.ShowDialog<bool?>(desktop.MainWindow);
                
                if (result == true)
                {
                    return (true, dialog.Reason, dialog.Note, dialog.SelectedFilePath);
                }
            }
            return (false, null, null, null);
        }
        public async Task<(bool Confirmed, TimeSpan? InTime, TimeSpan? OutTime)> ShowEditAttendanceAsync(TimeSpan? currentIn, TimeSpan? currentOut, bool showIn = true, bool showOut = true)
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
            {
                var dialog = new OCC.Client.Features.TimeAttendanceHub.Views.EditAttendanceDialog(currentIn, currentOut, showIn, showOut);
                var result = await dialog.ShowDialog<bool>(desktop.MainWindow);
                
                if (result)
                {
                    return (true, dialog.ClockInTime, dialog.ClockOutTime);
                }
            }
            return (false, null, null);
        }

        public async Task<bool> ShowSessionTimeoutAsync()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
            {
                var dialog = new OCC.Client.Views.Confirmation.SessionTimeoutDialog();
                // Show as Modal?
                // Depending on requirements, maybe ShowDialog is best to block input.
                var result = await dialog.ShowDialog<bool>(desktop.MainWindow);
                return result;
            }
            return false;
        }

        public async Task<string?> ShowInputAsync(string title, string message, string defaultValue = "")
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
            {
                var dialog = new OCC.Client.Views.TextInputDialog(title, message, defaultValue);
                var result = await dialog.ShowDialog<string?>(desktop.MainWindow);
                return result;
            }
            return null;
        }

        public async Task<OCC.Shared.Models.EmployeeLoan?> ShowAddLoanAsync()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
            {
                var dialog = new OCC.Client.Features.WagesHub.Views.AddLoanDialog();
                var vm = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<OCC.Client.Features.WagesHub.ViewModels.AddLoanDialogViewModel>(_serviceProvider);
                vm.CloseAction = (loan) => Avalonia.Threading.Dispatcher.UIThread.Post(() => dialog.Close(loan));
                dialog.DataContext = vm;
                var result = await dialog.ShowDialog<OCC.Shared.Models.EmployeeLoan?>(desktop.MainWindow);
                return result;
            }
            return null;
        }

        public async Task<LeaveRequest?> ShowEditLeaveRequestAsync(LeaveRequest request)
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
            {
                var dialog = new OCC.Client.Features.TimeAttendanceHub.Views.EmployeeLeaveDialog(request);
                return await dialog.ShowDialog<LeaveRequest?>(desktop.MainWindow);
            }
            return null;
        }
    }
}
