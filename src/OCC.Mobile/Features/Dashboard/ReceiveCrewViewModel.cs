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
using Avalonia.Media;
using Avalonia;

namespace OCC.Mobile.Features.Dashboard
{
    public partial class SiteDeploymentMemberCardViewModel : ObservableObject
    {
        public SiteDeploymentMemberDto Member { get; }

        public Guid Id => Member.Id;
        public Guid EmployeeId => Member.EmployeeId;
        public string FullName => Member.FullName;
        public string Role => Member.Role;
        public string Initials => Member.Initials;

        [ObservableProperty]
        private string _attendance = "Pending"; // "Pending", "Present", "Absent"

        [ObservableProperty]
        private string? _arrivedAt;

        [ObservableProperty]
        private bool _isSelected;

        public IBrush AvatarBrush { get; }

        public SiteDeploymentMemberCardViewModel(SiteDeploymentMemberDto member, int index)
        {
            Member = member;
            
            if (member.IsAbsent)
            {
                _attendance = "Absent";
                _arrivedAt = null;
            }
            else
            {
                // Default to Pending for daily receipt sheet validation
                _attendance = "Pending";
                _arrivedAt = null;
            }

            // Define gradients matching premium tailwind badges
            var gradients = new[]
            {
                new { From = "#6366F1", To = "#8B5CF6" }, // Indigo to Violet
                new { From = "#10B981", To = "#14B8A6" }, // Emerald to Teal
                new { From = "#F59E0B", To = "#F97316" }, // Amber to Orange
                new { From = "#06B6D4", To = "#0EA5E9" }, // Cyan to Sky
                new { From = "#8B5CF6", To = "#A855F7" }, // Violet to Purple
                new { From = "#F43F5E", To = "#EC4899" }, // Rose to Pink
                new { From = "#14B8A6", To = "#10B981" }, // Teal to Emerald
                new { From = "#0EA5E9", To = "#6366F1" }  // Sky to Indigo
            };

            var selectedGrad = gradients[index % gradients.Length];
            AvatarBrush = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops = new GradientStops
                {
                    new GradientStop(Color.Parse(selectedGrad.From), 0),
                    new GradientStop(Color.Parse(selectedGrad.To), 1)
                }
            };
        }

        partial void OnAttendanceChanged(string value)
        {
            Member.IsAbsent = value == "Absent";
        }
    }

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
        private ObservableCollection<SiteDeploymentMemberCardViewModel> _crewMembers = new();

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

        [ObservableProperty]
        private bool _bulkMode;

        [ObservableProperty]
        private Guid? _targetProjectId;

        public bool HasSelectedDeployment => SelectedDeployment != null;

        public int TotalCount => CrewMembers.Count;
        public int PresentCount => CrewMembers.Count(m => m.Attendance == "Present");
        public int AbsentCount => CrewMembers.Count(m => m.Attendance == "Absent");
        public int PendingCount => CrewMembers.Count(m => m.Attendance == "Pending");
        public bool AllMarked => PendingCount == 0;

        public int SelectedCount => CrewMembers.Count(m => m.IsSelected);

        public string TimeString => DateTime.Now.ToString("HH:mm");
        public string DateString => DateTime.Now.ToString("ddd dd MMM yyyy");

        public ObservableCollection<SiteDeploymentMemberCardViewModel> PresentMembers => 
            new(CrewMembers.Where(m => m.Attendance == "Present"));

        public ObservableCollection<SiteDeploymentMemberCardViewModel> AbsentMembers => 
            new(CrewMembers.Where(m => m.Attendance == "Absent"));

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
            Title = "Crew Attendance";

            PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SelectedDeployment) || e.PropertyName == nameof(SimulateGpsError))
                {
                    UpdateGpsCheck();
                }
            };
        }

        [RelayCommand]
        public async Task LoadDataAsync()
        {
            if (IsLoading) return;
            IsLoading = true;
            StatusMessage = "";
            ShowSuccessState = false;
            BulkMode = false;

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
                    if (TargetProjectId.HasValue)
                    {
                        var matching = PendingDeployments.FirstOrDefault(d => d.ProjectId == TargetProjectId.Value);
                        SelectedDeployment = matching ?? PendingDeployments.First();
                    }
                    else
                    {
                        SelectedDeployment = PendingDeployments.First();
                    }
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
            foreach (var m in CrewMembers)
            {
                m.PropertyChanged -= OnMemberPropertyChanged;
            }

            CrewMembers.Clear();
            if (value != null)
            {
                for (int i = 0; i < value.Members.Count; i++)
                {
                    var card = new SiteDeploymentMemberCardViewModel(value.Members[i], i);
                    card.PropertyChanged += OnMemberPropertyChanged;
                    CrewMembers.Add(card);
                }
            }
            
            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(PresentCount));
            OnPropertyChanged(nameof(AbsentCount));
            OnPropertyChanged(nameof(PendingCount));
            OnPropertyChanged(nameof(AllMarked));
            OnPropertyChanged(nameof(SelectedCount));
            OnPropertyChanged(nameof(PresentMembers));
            OnPropertyChanged(nameof(AbsentMembers));
            
            ConfirmReceivedCommand.NotifyCanExecuteChanged();
        }

        private void OnMemberPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SiteDeploymentMemberCardViewModel.Attendance) || 
                e.PropertyName == nameof(SiteDeploymentMemberCardViewModel.IsSelected))
            {
                OnPropertyChanged(nameof(PresentCount));
                OnPropertyChanged(nameof(AbsentCount));
                OnPropertyChanged(nameof(PendingCount));
                OnPropertyChanged(nameof(AllMarked));
                OnPropertyChanged(nameof(SelectedCount));
                OnPropertyChanged(nameof(PresentMembers));
                OnPropertyChanged(nameof(AbsentMembers));
                
                ConfirmReceivedCommand.NotifyCanExecuteChanged();
                MarkSelectedAbsentCommand.NotifyCanExecuteChanged();
            }
        }

        [RelayCommand]
        private void SetMemberPresent(SiteDeploymentMemberCardViewModel member)
        {
            if (member != null)
            {
                if (member.Attendance == "Present")
                {
                    member.Attendance = "Pending";
                    member.ArrivedAt = null;
                }
                else
                {
                    member.Attendance = "Present";
                    member.ArrivedAt = TimeString;
                }
            }
        }

        [RelayCommand]
        private void SetMemberAbsent(SiteDeploymentMemberCardViewModel member)
        {
            if (member != null)
            {
                if (member.Attendance == "Absent")
                {
                    member.Attendance = "Pending";
                    member.ArrivedAt = null;
                }
                else
                {
                    member.Attendance = "Absent";
                    member.ArrivedAt = null;
                }
            }
        }

        [RelayCommand]
        private void MarkAllPresent()
        {
            var now = TimeString;
            foreach (var m in CrewMembers)
            {
                if (m.Attendance == "Pending")
                {
                    m.Attendance = "Present";
                    m.ArrivedAt = now;
                }
            }
        }

        [RelayCommand]
        private void ToggleBulkMode()
        {
            BulkMode = !BulkMode;
            if (!BulkMode)
            {
                foreach (var m in CrewMembers)
                {
                    m.IsSelected = false;
                }
            }
        }

        [RelayCommand]
        private void ToggleSelectMember(SiteDeploymentMemberCardViewModel member)
        {
            if (member != null)
            {
                member.IsSelected = !member.IsSelected;
            }
        }

        public bool CanMarkSelectedAbsent => BulkMode && SelectedCount > 0;

        [RelayCommand(CanExecute = nameof(CanMarkSelectedAbsent))]
        private void MarkSelectedAbsent()
        {
            foreach (var m in CrewMembers.Where(m => m.IsSelected))
            {
                m.Attendance = "Absent";
                m.ArrivedAt = null;
                m.IsSelected = false;
            }
            BulkMode = false;
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

        [RelayCommand(CanExecute = nameof(AllMarked))]
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

                double? lat = SelectedDeployment.ProjectLatitude;
                double? lon = SelectedDeployment.ProjectLongitude;
                if (SimulateGpsError && lat.HasValue && lon.HasValue)
                {
                    lat += 0.01;
                    lon += 0.01;
                }

                var request = new ReceiveDeploymentRequest
                {
                    SiteManagerId = smId.Value,
                    AbsentMemberEmployeeIds = CrewMembers.Where(m => m.Attendance == "Absent").Select(m => m.EmployeeId).ToList(),
                    GpsLatitude = lat,
                    GpsLongitude = lon
                };

                var success = await _deploymentService.ReceiveDeploymentAsync(SelectedDeployment.Id, request);
                if (success)
                {
                    ShowSuccessState = true;
                    StatusMessage = "";
                    OnPropertyChanged(nameof(PresentMembers));
                    OnPropertyChanged(nameof(AbsentMembers));
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
            _navigationService.NavigateTo<ActiveProjectsViewModel>();
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
