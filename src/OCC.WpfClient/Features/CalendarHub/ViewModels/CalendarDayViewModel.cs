using CommunityToolkit.Mvvm.ComponentModel;
using OCC.WpfClient.Features.CalendarHub.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace OCC.WpfClient.Features.CalendarHub.ViewModels
{
    // =========================================================================
    // CalendarDayViewModel.cs
    // Represents a single cell in the 7×6 calendar grid.
    // Each day holds its date metadata and a capped list of CalendarEvent objects.
    // =========================================================================

    /// <summary>
    /// ViewModel for a single day cell in the calendar grid.
    /// Exposes both the full event list and a display-capped subset so the UI
    /// can show at most <see cref="MaxVisibleEvents"/> bars before showing overflow.
    /// </summary>
    public partial class CalendarDayViewModel : ObservableObject
    {
        #region Constants

        /// <summary>
        /// Maximum number of event bars rendered directly in the day cell.
        /// Events beyond this limit are accessible via the "+N more" overflow popup.
        /// </summary>
        public const int MaxVisibleEvents = 3;

        #endregion

        #region Read-Only Properties

        /// <summary>The calendar date this cell represents.</summary>
        public DateTime Date { get; }

        /// <summary>The day number (1–31) shown in the top-left of the cell.</summary>
        public int DayNumber => Date.Day;

        /// <summary>
        /// <c>true</c> if this cell belongs to the currently displayed month.
        /// Cells from the previous/next month are shown dimmed as padding.
        /// </summary>
        public bool IsCurrentMonth { get; }

        /// <summary>Inverse of <see cref="IsCurrentMonth"/> — used for XAML opacity bindings.</summary>
        public bool IsNotCurrentMonth => !IsCurrentMonth;

        /// <summary><c>true</c> if this cell represents today's date.</summary>
        public bool IsToday { get; }

        /// <summary><c>true</c> if the date falls on a Saturday or Sunday.</summary>
        public bool IsWeekend => Date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

        #endregion

        #region Observable Properties

        /// <summary><c>true</c> when this cell is the user's currently selected day.</summary>
        [ObservableProperty]
        private bool _isSelected;

        /// <summary>
        /// <c>true</c> when this cell contains at least one public holiday event.
        /// Used to highlight the cell header with a red accent.
        /// </summary>
        [ObservableProperty]
        private bool _isHoliday;

        /// <summary>
        /// The name of the public holiday, shown next to the day number.
        /// Only populated when <see cref="IsHoliday"/> is <c>true</c>.
        /// </summary>
        [ObservableProperty]
        private string? _holidayName;

        #endregion

        #region Event Collections

        /// <summary>
        /// All calendar events assigned to this day (unlimited).
        /// Populated by <c>CalendarHubViewModel.GenerateCalendar()</c>.
        /// </summary>
        public ObservableCollection<CalendarEvent> Events { get; } = new();

        /// <summary>
        /// Subset of <see cref="Events"/> capped at <see cref="MaxVisibleEvents"/>,
        /// used as the ItemsSource for the in-cell event bars.
        /// </summary>
        public ObservableCollection<CalendarEvent> VisibleEvents { get; } = new();

        /// <summary>
        /// Events that exceed the visible cap — shown in the overflow popup.
        /// </summary>
        public ObservableCollection<CalendarEvent> OverflowEvents { get; } = new();

        /// <summary>Number of events hidden behind the "+N more" overflow button.</summary>
        public int OverflowCount => OverflowEvents.Count;

        /// <summary><c>true</c> when there are more events than can be displayed inline.</summary>
        public bool HasOverflow => OverflowCount > 0;

        /// <summary><c>true</c> when the overflow popup is currently open.</summary>
        [ObservableProperty]
        private bool _isOverflowOpen;

        #endregion

        #region Constructor

        /// <summary>
        /// Initialises a calendar day cell.
        /// </summary>
        /// <param name="date">The date this cell represents.</param>
        /// <param name="isCurrentMonth"><c>true</c> if the date is in the displayed month.</param>
        public CalendarDayViewModel(DateTime date, bool isCurrentMonth)
        {
            Date           = date;
            IsCurrentMonth = isCurrentMonth;
            IsToday        = date.Date == DateTime.Today;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Re-computes <see cref="VisibleEvents"/> and <see cref="OverflowEvents"/>
        /// from the current <see cref="Events"/> list.
        /// Call this after adding/removing events in bulk.
        /// </summary>
        public void RefreshVisibleEvents()
        {
            VisibleEvents.Clear();
            OverflowEvents.Clear();

            var ordered = Events
                .OrderBy(e => (int)e.Type)   // Holidays first, then tasks, then birthdays, etc.
                .ThenBy(e => e.StartDate)
                .ToList();

            for (int i = 0; i < ordered.Count; i++)
            {
                if (i < MaxVisibleEvents)
                    VisibleEvents.Add(ordered[i]);
                else
                    OverflowEvents.Add(ordered[i]);
            }

            // Notify computed properties that depend on the collections
            OnPropertyChanged(nameof(OverflowCount));
            OnPropertyChanged(nameof(HasOverflow));
        }

        #endregion
    }
}
