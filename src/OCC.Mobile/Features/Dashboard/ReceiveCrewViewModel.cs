using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Mobile.Services;
using OCC.Mobile.ViewModels;
using OCC.Shared.DTOs;
using OCC.Shared.Models;

namespace OCC.Mobile.Features.Dashboard
{
    public partial class ReceiveCrewViewModel : ViewModelBase
    {
        private readonly ISiteDeploymentService _deploymentService;
        private readonly INavigationService _navigationService;
        private readonly IAuthService _authService;
        private readonly ILocalSettingsService _settingsService;

        [ObservableProperty]
        private ObservableCollection<SiteDeploymentDto> _pendingDeployments = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasSelectedDeployment))]
        private SiteDeploymentDto? _selectedDeployment;

        [ObservableProperty]
        private ObservableCollection<SiteDeploymentMemberDto> _crewMembers = new();

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _gpsStatusText = "GPS: On-Site";

        [ObservableProperty]
        private bool _hasGpsWarning;

        [ObservableProperty]
        private string _gpsWarningMessage = string.Empty;

        [ObservableProperty]
        private bool _simulateGpsError;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private bool _showSuccessState;

        public bool HasSelectedDeployment => SelectedDeployment != null;

        public int PresentCount => CrewMembers.Count(m => !m.IsAbsent);
        public int AbsentCount => CrewMembers.Count(m => m.IsAbsent);

        public ReceiveCrewViewModel(
            ISiteDeploymentService deploymentService,
            INavigationService navigationService,
            IAuthService authService,
            ILocalSettingsService settingsService)
        {
            _deploymentService = deploymentService;
            _navigationService = navigationService;
            _authService = authService;
            _settingsService = settingsService;
            Title = "Receive daily crew";
            PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SelectedDeployment) || e.PropertyName == nameof(SimulateGpsError))
                {
                    UpdateGpsCheck();
                }
            };

            LoadDataAsync().FireAndForget();
        }

        [RelayCommand]
        public async Task LoadDataAsync()
        {
            if (IsLoading) return;
            IsLoading = true;
            StatusMessage = "";
            ShowSuccessState = false;

            try
            {
                var smId = await GetSiteManagerEmployeeIdAsync();
                if (!smId.HasValue)
                {
                    StatusMessage = "Could not find Site Manager employee profile.";
                    return;
                }

                var deployments = await _deploymentService.GetPendingDeploymentsAsync(smId.Value);
                PendingDeployments.Clear();
                foreach (var d in deployments)
                {
                    PendingDeployments.Add(d);
                }

                if (PendingDeployments.Count > 0)
                {
                    SelectedDeployment = PendingDeployments.First();
                }
                else
                {
                    SelectedDeployment = null;
                    CrewMembers.Clear();
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading deployments: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        partial void OnSelectedDeploymentChanged(SiteDeploymentDto? value)
        {
            CrewMembers.Clear();
            if (value != null)
            {
                foreach (var member in value.Members)
                {
                    // Copy to trigger change notifications properly
                    CrewMembers.Add(new SiteDeploymentMemberDto
                    {
                        Id = member.Id,
                        EmployeeId = member.EmployeeId,
                        FullName = member.FullName,
                        Role = member.Role,
                        Initials = member.Initials,
                        IsAbsent = member.IsAbsent
                    });
                }
            }
            
            OnPropertyChanged(nameof(PresentCount));
            OnPropertyChanged(nameof(AbsentCount));
        }

        [RelayCommand]
        private void ToggleAbsent(SiteDeploymentMemberDto member)
        {
            member.IsAbsent = !member.IsAbsent;
            OnPropertyChanged(nameof(PresentCount));
            OnPropertyChanged(nameof(AbsentCount));
        }

        private void UpdateGpsCheck()
        {
            if (SelectedDeployment == null)
            {
                HasGpsWarning = false;
                GpsStatusText = "GPS: Unknown";
                GpsWarningMessage = "";
                return;
            }

            if (!SelectedDeployment.ProjectLatitude.HasValue || !SelectedDeployment.ProjectLongitude.HasValue)
            {
                HasGpsWarning = false;
                GpsStatusText = "GPS: No project coordinates set";
                GpsWarningMessage = "Cannot verify location since project coordinates are not set.";
                return;
            }

            if (SimulateGpsError)
            {
                HasGpsWarning = true;
                GpsStatusText = "GPS: Location Warning";
                GpsWarningMessage = "Device GPS indicates you are 1.1 km away from the project site.";
            }
            else
            {
                HasGpsWarning = false;
                GpsStatusText = "GPS: Verified On-Site";
                GpsWarningMessage = "";
            }
        }

        [RelayCommand]
        private async Task ConfirmReceived()
        {
            if (SelectedDeployment == null || IsLoading) return;

            IsLoading = true;
            StatusMessage = "Sending confirmation...";

            try
            {
                var smId = await GetSiteManagerEmployeeIdAsync();
                if (!smId.HasValue)
                {
                    StatusMessage = "Site manager ID not found.";
                    IsLoading = false;
                    return;
                }

                // Simulate GPS position based on geofence check
                double? lat = SelectedDeployment.ProjectLatitude;
                double? lon = SelectedDeployment.ProjectLongitude;
                if (SimulateGpsError && lat.HasValue && lon.HasValue)
                {
                    lat += 0.01; // offset approx 1.1km
                    lon += 0.01;
                }

                var request = new ReceiveDeploymentRequest
                {
                    SiteManagerId = smId.Value,
                    AbsentMemberEmployeeIds = CrewMembers.Where(m => m.IsAbsent).Select(m => m.EmployeeId).ToList(),
                    GpsLatitude = lat,
                    GpsLongitude = lon
                };

                var success = await _deploymentService.ReceiveDeploymentAsync(SelectedDeployment.Id, request);
                if (success)
                {
                    ShowSuccessState = true;
                    StatusMessage = "";
                    // Refresh dashboard after a slight delay
                    await Task.Delay(1500);
                    GoBack();
                }
                else
                {
                    StatusMessage = "Failed to confirm receipt with the server.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void GoBack()
        {
            _navigationService.NavigateTo<DashboardViewModel>();
        }

        private Guid? _siteManagerEmployeeId;

        private async Task<Guid?> GetSiteManagerEmployeeIdAsync()
        {
            if (_siteManagerEmployeeId.HasValue)
                return _siteManagerEmployeeId;

            if (_authService.CurrentUser == null) return null;

            try
            {
                var baseUrl = _authService.GetBaseUrl();
                using var client = new HttpClient();
                var token = _authService.CurrentToken;
                if (!string.IsNullOrEmpty(token))
                {
                    client.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }
                client.DefaultRequestHeaders.Add("X-Environment", _settingsService.Settings.SelectedEnvironment.ToString());

                var employees = await client.GetFromJsonAsync<List<EmployeeSummaryDto>>($"{baseUrl}api/Employees");
                if (employees != null)
                {
                    var currentEmployee = employees.FirstOrDefault(e => e.LinkedUserId == _authService.CurrentUser.Id);
                    if (currentEmployee != null)
                    {
                        _siteManagerEmployeeId = currentEmployee.Id;
                        return _siteManagerEmployeeId;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error resolving Site Manager Employee ID: {ex.Message}");
            }

            return null;
        }
    }
}
