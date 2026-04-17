using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OCC.Client.Services.Interfaces;
using OCC.Client.ViewModels.Core;
using OCC.Client.ViewModels.Messages;
using OCC.Shared.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.Client.Features.TimeAttendanceHub.ViewModels
{
    public partial class TimeLiveV2ViewModel : ViewModelBase, IRecipient<EntityUpdatedMessage>
    {
        private readonly ITimeServiceV2 _timeServiceV2;
        private readonly IEmployeeService _employeeService;
        private readonly IDialogService _dialogService;

        [ObservableProperty]
        private ObservableCollection<LiveV2EventViewModel> _recentEvents = new();

        [ObservableProperty]
        private bool _isLoading;

        public TimeLiveV2ViewModel(ITimeServiceV2 timeServiceV2, IEmployeeService employeeService, IDialogService dialogService)
        {
            _timeServiceV2 = timeServiceV2;
            _employeeService = employeeService;
            _dialogService = dialogService;

            Title = "Live Board V2 (Physical)";
            WeakReferenceMessenger.Default.Register<EntityUpdatedMessage>(this);
        }

        public async Task LoadDataAsync()
        {
            if (IsLoading) return;
            IsLoading = true;

            try
            {
                var activePresence = await _timeServiceV2.GetActivePhysicalPresenceAsync();
                var employees = await _employeeService.GetEmployeesAsync();

                var eventVms = activePresence.Select(e => 
                {
                    var emp = employees.FirstOrDefault(emp => emp.Id == e.EmployeeId);
                    return new LiveV2EventViewModel
                    {
                        EventId = e.Id,
                        EmployeeId = e.EmployeeId,
                        EmployeeName = emp != null ? $"{emp.FirstName} {emp.LastName}" : "Unknown",
                        EventType = e.EventType,
                        Timestamp = e.Timestamp,
                        Source = e.Source
                    };
                }).OrderByDescending(x => x.Timestamp).ToList();

                RecentEvents = new ObservableCollection<LiveV2EventViewModel>(eventVms);
            }
            catch (Exception ex)
            {
                 System.Diagnostics.Debug.WriteLine($"[TimeLiveV2] Load Error: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        public async Task RepairSyncV2()
        {
            if (IsLoading) return;
            IsLoading = true;
            try
            {
                var result = await _timeServiceV2.RepairSyncV2Async();
                if (result.Success)
                {
                    await _dialogService.ShowAlertAsync("Repair Complete", result.Message);
                    await LoadDataAsync();
                }
                else
                {
                    await _dialogService.ShowAlertAsync("Repair Failed", result.Message);
                }
            }
            catch (Exception ex)
            {
                await _dialogService.ShowAlertAsync("Error", $"Repair failed: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async void Receive(EntityUpdatedMessage message)
        {
            if (message.Value.EntityType == "ClockingEvent")
            {
                // Simple refresh for now
                await LoadDataAsync();
            }
        }
    }

    public partial class LiveV2EventViewModel : ObservableObject
    {
        public Guid EventId { get; set; }
        public Guid EmployeeId { get; set; }
        
        [ObservableProperty]
        private string _employeeName = string.Empty;
        
        [ObservableProperty]
        private ClockEventType _eventType;
        
        [ObservableProperty]
        private DateTime _timestamp;
        
        [ObservableProperty]
        private string _source = string.Empty;
    }
}
