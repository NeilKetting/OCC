using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Client.Services;
using System;
using System.Threading.Tasks;
using Velopack;

using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Managers.Interfaces;
using OCC.Client.Services.Repositories.Interfaces;
using OCC.Client.ViewModels.Core;

namespace OCC.Client.ViewModels.Shared
{
    public partial class UpdateDialogViewModel : ViewModelBase
    {
        private readonly IUpdateService _updateService;
        private readonly UpdateInfo _updateInfo;
        private readonly Action _closeAction;

        [ObservableProperty]
        private string _newVersion;

        [ObservableProperty]
        private string _releaseNotes; // Velopack 0.0.95+ might not expose release notes easily in UpdateInfo yet, so we might just show version.

        [ObservableProperty]
        private bool _isDownloading;

        [ObservableProperty]
        private int _progress;

        [ObservableProperty]
        private string _statusText;

        public UpdateDialogViewModel()
        {
            // Parameterless constructor for design-time support
            _updateService = null!;
            _updateInfo = null!;
            _closeAction = null!;
            _newVersion = string.Empty;
            _releaseNotes = string.Empty;
            _statusText = string.Empty;
        }
        public UpdateDialogViewModel(IUpdateService updateService, UpdateInfo updateInfo, Action closeAction)
        {
            _updateService = updateService;
            _updateInfo = updateInfo;
            _closeAction = closeAction;

            _newVersion = updateInfo.TargetFullRelease.Version.ToString();
            _statusText = "A new version of OCC Client is available.";
            _releaseNotes = "New features and bug fixes.";
        }

        [RelayCommand]
        private async Task StartUpdate()
        {
            IsDownloading = true;
            StatusText = "Downloading update...";

            try
            {
                await _updateService.DownloadUpdatesAsync(_updateInfo, (p) =>
                {
                    Progress = p;
                });

                StatusText = "Installing...";
                _updateService.ApplyUpdatesAndExit(_updateInfo);
            }
            catch (Exception ex)
            {
                StatusText = "Update failed: " + ex.Message;
                IsDownloading = false;
            }
        }

        [RelayCommand]
        private void Close()
        {
            _closeAction?.Invoke();
        }
    }
}
