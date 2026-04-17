using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OCC.Client.Features.CalendarHub.Models;
using OCC.Client.Services.Infrastructure;
using OCC.Client.Services.Interfaces;
using OCC.Client.ViewModels.Core;
using OCC.Client.ViewModels.Messages;

namespace OCC.Client.Features.CalendarHub.ViewModels
{
    public partial class CalendarHubViewModel : ViewModelBase, IRecipient<TaskUpdatedMessage>
    {
        private readonly ICalendarService _calendarService;
        private readonly UserPreferencesService _preferencesService;

        [ObservableProperty]
        private DateTime _currentMonth;

        [ObservableProperty]
        private string _monthName = string.Empty;

        [ObservableProperty]
        private string _yearName = string.Empty;

        [ObservableProperty]
        private ObservableCollection<CalendarDayViewModel> _days = new();

        [ObservableProperty]
        private ObservableCollection<string> _weekDays = new() { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };

        #region Filter Properties

        public bool ShowTasks
        {
            get => _preferencesService.Preferences.ShowTasks;
            set
            {
                if (_preferencesService.Preferences.ShowTasks != value)
                {
                    _preferencesService.Preferences.ShowTasks = value;
                    _preferencesService.SavePreferences();
                    OnPropertyChanged();
                    GenerateCalendar();
                }
            }
        }

        public bool ShowMeetings
        {
            get => _preferencesService.Preferences.ShowMeetings;
            set
            {
                if (_preferencesService.Preferences.ShowMeetings != value)
                {
                    _preferencesService.Preferences.ShowMeetings = value;
                    _preferencesService.SavePreferences();
                    OnPropertyChanged();
                    GenerateCalendar();
                }
            }
        }

        public bool ShowToDos
        {
            get => _preferencesService.Preferences.ShowToDos;
            set
            {
                if (_preferencesService.Preferences.ShowToDos != value)
                {
                    _preferencesService.Preferences.ShowToDos = value;
                    _preferencesService.SavePreferences();
                    OnPropertyChanged();
                    GenerateCalendar();
                }
            }
        }

        public bool ShowBirthdays
        {
            get => _preferencesService.Preferences.ShowBirthdays;
            set
            {
                if (_preferencesService.Preferences.ShowBirthdays != value)
                {
                    _preferencesService.Preferences.ShowBirthdays = value;
                    _preferencesService.SavePreferences();
                    OnPropertyChanged();
                    GenerateCalendar();
                }
            }
        }

        public bool ShowPublicHolidays
        {
            get => _preferencesService.Preferences.ShowPublicHolidays;
            set
            {
                if (_preferencesService.Preferences.ShowPublicHolidays != value)
                {
                    _preferencesService.Preferences.ShowPublicHolidays = value;
                    _preferencesService.SavePreferences();
                    OnPropertyChanged();
                    GenerateCalendar();
                }
            }
        }

        public bool ShowLeave
        {
            get => _preferencesService.Preferences.ShowLeave;
            set
            {
                if (_preferencesService.Preferences.ShowLeave != value)
                {
                    _preferencesService.Preferences.ShowLeave = value;
                    _preferencesService.SavePreferences();
                    OnPropertyChanged();
                    GenerateCalendar();
                }
            }
        }

        public bool ShowOrderDeliveries
        {
            get => _preferencesService.Preferences.ShowOrderDeliveries;
            set
            {
                if (_preferencesService.Preferences.ShowOrderDeliveries != value)
                {
                    _preferencesService.Preferences.ShowOrderDeliveries = value;
                    _preferencesService.SavePreferences();
                    OnPropertyChanged();
                    GenerateCalendar();
                }
            }
        }

        [ObservableProperty]
        private CalendarDayViewModel? _selectedDay;

        #endregion

        public CalendarHubViewModel(
            ICalendarService calendarService,
            UserPreferencesService preferencesService)
        {
            _calendarService = calendarService;
            _preferencesService = preferencesService;

            CurrentMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            WeakReferenceMessenger.Default.Register(this);
            
            GenerateCalendar();
        }

        public void Receive(TaskUpdatedMessage message)
        {
            GenerateCalendar();
        }

        [RelayCommand]
        private void NextMonth()
        {
            CurrentMonth = CurrentMonth.AddMonths(1);
            GenerateCalendar();
        }

        [RelayCommand]
        private void PreviousMonth()
        {
            CurrentMonth = CurrentMonth.AddMonths(-1);
            GenerateCalendar();
        }

        [RelayCommand]
        private void GoToToday()
        {
            CurrentMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            GenerateCalendar();
        }

        [RelayCommand]
        private void SelectDay(CalendarDayViewModel day)
        {
            if (SelectedDay != null) SelectedDay.IsSelected = false;
            SelectedDay = day;
            SelectedDay.IsSelected = true;
        }

        [RelayCommand]
        private void CreateTask(CalendarDayViewModel day)
        {
            if (day == null) return;
            WeakReferenceMessenger.Default.Send(new OCC.Client.ViewModels.Messages.CreateNewTaskMessage(null, day.Date));
        }

        private async void GenerateCalendar()
        {
            MonthName = CurrentMonth.ToString("MMMM");
            YearName = CurrentMonth.ToString("yyyy");

            var firstDayOfMonth = new DateTime(CurrentMonth.Year, CurrentMonth.Month, 1);
            var daysInMonth = DateTime.DaysInMonth(CurrentMonth.Year, CurrentMonth.Month);

            int offset = (int)firstDayOfMonth.DayOfWeek - 1;
            if (offset < 0) offset = 6;

            var dayList = new List<CalendarDayViewModel>();

            // Padding: Previous Month
            var prevMonth = CurrentMonth.AddMonths(-1);
            var daysInPrevMonth = DateTime.DaysInMonth(prevMonth.Year, prevMonth.Month);
            for (int i = 0; i < offset; i++)
            {
                var d = daysInPrevMonth - offset + 1 + i;
                dayList.Add(new CalendarDayViewModel(new DateTime(prevMonth.Year, prevMonth.Month, d), false));
            }

            // Current Month
            for (int i = 1; i <= daysInMonth; i++)
            {
                dayList.Add(new CalendarDayViewModel(new DateTime(CurrentMonth.Year, CurrentMonth.Month, i), true));
            }

            // Padding: Next Month
            int remaining = 42 - dayList.Count;
            var nextMonth = CurrentMonth.AddMonths(1);
            for (int i = 1; i <= remaining; i++)
            {
                dayList.Add(new CalendarDayViewModel(new DateTime(nextMonth.Year, nextMonth.Month, i), false));
            }

            // Fetch Events
            var events = await _calendarService.GetEventsAsync(dayList.First().Date, dayList.Last().Date);

            // Filter Events
            var filteredEvents = events.Where(e =>
                (e.Type == CalendarEventType.Task && ShowTasks) ||
                (e.Type == CalendarEventType.Meeting && ShowMeetings) ||
                (e.Type == CalendarEventType.ToDo && ShowToDos) ||
                (e.Type == CalendarEventType.Birthday && ShowBirthdays) ||
                (e.Type == CalendarEventType.PublicHoliday && ShowPublicHolidays) ||
                (e.Type == CalendarEventType.Leave && ShowLeave) ||
                (e.Type == CalendarEventType.OrderDelivery && ShowOrderDeliveries)
            ).ToList();

            // Populate Days with Spanning Logic
            foreach (var evt in filteredEvents)
            {
                foreach (var day in dayList)
                {
                    if (day.Date.Date >= evt.StartDate.Date && day.Date.Date <= evt.EndDate.Date)
                    {
                        var spanEvt = new CalendarEvent
                        {
                            Id = evt.Id,
                            Type = evt.Type,
                            Title = evt.Title,
                            Description = evt.Description,
                            StartDate = evt.StartDate,
                            EndDate = evt.EndDate,
                            Color = evt.Color,
                            IsCompleted = evt.IsCompleted,
                            OriginalSource = evt.OriginalSource
                        };

                        bool isStart = day.Date.Date == evt.StartDate.Date;
                        bool isEnd = day.Date.Date == evt.EndDate.Date;

                        if (isStart && isEnd) spanEvt.Span = CalendarEventSpan.Single;
                        else if (isStart) spanEvt.Span = CalendarEventSpan.Start;
                        else if (isEnd) spanEvt.Span = CalendarEventSpan.End;
                        else spanEvt.Span = CalendarEventSpan.Middle;

                        day.Events.Add(spanEvt);

                        // If it's a holiday, mark the day
                        if (evt.Type == CalendarEventType.PublicHoliday)
                        {
                            day.IsHoliday = true;
                            day.HolidayName = evt.Title;
                        }
                    }
                }
            }

            SelectedDay = dayList.FirstOrDefault(d => d.IsToday) ?? dayList.FirstOrDefault(d => d.IsCurrentMonth);
            if (SelectedDay != null) SelectedDay.IsSelected = true;

            Days.Clear();
            foreach (var d in dayList) Days.Add(d);
        }
    }
}
