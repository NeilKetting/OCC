using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Client.Services.Interfaces;
using OCC.Client.ViewModels.Core;
using OCC.Shared.Enums;
using OCC.Shared.Models;
using System;
using System.Threading.Tasks;

namespace OCC.Client.Mobile.Features.Dashboard
{
    public partial class ReminderSettingsViewModel : ViewModelBase
    {
        private readonly Services.Repositories.Interfaces.IRepository<ProjectTask> _taskRepository;
        private readonly INotificationService _notificationService;
        private readonly ProjectTask _task;

        [ObservableProperty]
        private bool _isReminderEnabled;

        [ObservableProperty]
        private ReminderFrequency _frequency;

        [ObservableProperty]
        private DateTime _reminderDate;

        [ObservableProperty]
        private TimeSpan _reminderTime;

        public Array Frequencies => Enum.GetValues(typeof(ReminderFrequency));

        public ReminderSettingsViewModel(ProjectTask task, Services.Repositories.Interfaces.IRepository<ProjectTask> taskRepository, INotificationService notificationService)
        {
            _task = task;
            _taskRepository = taskRepository;
            _notificationService = notificationService;
            
            Title = $"Reminders: {task.Name}";
            
            IsReminderEnabled = task.IsReminderSet;
            Frequency = task.Frequency;
            ReminderDate = task.NextReminderDate?.Date ?? DateTime.Today;
            ReminderTime = task.NextReminderDate?.TimeOfDay ?? DateTime.Now.TimeOfDay;
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            _task.IsReminderSet = IsReminderEnabled;
            _task.Frequency = Frequency;
            
            if (IsReminderEnabled)
            {
                var triggerTime = ReminderDate.Date.Add(ReminderTime);
                _task.NextReminderDate = triggerTime;

                // Schedule on device
                await _notificationService.ScheduleAlarmAsync(
                    _task.Id, 
                    "Task Reminder", 
                    $"Don't forget: {_task.Name}", 
                    triggerTime);
            }
            else
            {
                _task.NextReminderDate = null;
                // TODO: Add CancelAlarmAsync to INotificationService if needed
            }

            await _taskRepository.UpdateAsync(_task);
        }
    }
}
