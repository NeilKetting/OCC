using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using OCC.Client.Features.CalendarHub.Models;

namespace OCC.Client.Features.CalendarHub.ViewModels
{
    public partial class CalendarDayViewModel : ObservableObject
    {
        public DateTime Date { get; }
        public int DayNumber => Date.Day;
        public bool IsCurrentMonth { get; }
        public bool IsNotCurrentMonth => !IsCurrentMonth;
        public bool IsToday { get; }
        public bool IsWeekend => Date.DayOfWeek == DayOfWeek.Saturday || Date.DayOfWeek == DayOfWeek.Sunday;
        
        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private bool _isHoliday;

        [ObservableProperty]
        private string? _holidayName;

        public ObservableCollection<CalendarEvent> Events { get; } = new();

        public CalendarDayViewModel(DateTime date, bool isCurrentMonth)
        {
            Date = date;
            IsCurrentMonth = isCurrentMonth;
            IsToday = date.Date == DateTime.Today;
        }
    }
}
