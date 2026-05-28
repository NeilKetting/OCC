using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OCC.WpfClient.Features.CalendarHub.Models;
using OCC.WpfClient.Features.CalendarHub.Services;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Infrastructure.Messages;
using OCC.WpfClient.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.WpfClient.Features.CalendarHub.ViewModels
{
    // =========================================================================
    // CalendarHubViewModel.cs
    // Main ViewModel for the unified Calendar screen.
    //
    // Responsibilities:
    //   • Owns the 42-cell day grid (6 rows × 7 columns, Mon–Sun)
    //   • Fetches events via ICalendarService scoped to the visible date window
    //   • Applies per-type visibility filters (Tasks / Holidays / Birthdays / Leave)
    //   • Supports optional project-level filtering for large portfolios
    //   • Responds to TaskUpdatedMessage to refresh the grid in real time
    // =========================================================================

    /// <summary>
    /// ViewModel for the CalendarHub view. Manages month navigation, event
    /// aggregation, filter state, and day-cell selection.
    /// </summary>
    public partial class CalendarHubViewModel
        : ViewModelBase,
          IRecipient<TaskUpdatedMessage>
    {
        #region Fields

        private readonly ICalendarService _calendarService;
        private readonly IProjectService  _projectService;

        /// <summary>
        /// Backing store for the ordered list of week-start day rows before being
        /// pushed into the observable <see cref="Days"/> collection.
        /// </summary>
        private List<CalendarDayViewModel> _dayList = new();

        #endregion

        #region Month Navigation Properties

        /// <summary>The first day of the month currently displayed.</summary>
        [ObservableProperty]
        private DateTime _currentMonth;

        /// <summary>Full month name (e.g. "May") shown in the header.</summary>
        [ObservableProperty]
        private string _monthName = string.Empty;

        /// <summary>Four-digit year string (e.g. "2026") shown in the header.</summary>
        [ObservableProperty]
        private string _yearName = string.Empty;

        #endregion

        #region Grid Collections

        /// <summary>
        /// The 42 day-cell ViewModels (6 weeks × 7 days) that back the UniformGrid.
        /// Includes padding cells from the previous and next months.
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<CalendarDayViewModel> _days = new();

        /// <summary>
        /// Weekday header labels displayed above the grid columns (Mon → Sun).
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<string> _weekDays = new()
            { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };

        /// <summary>The day cell the user has most recently clicked.</summary>
        [ObservableProperty]
        private CalendarDayViewModel? _selectedDay;

        #endregion

        #region Event-Type Filter Properties

        /// <summary>When <c>true</c>, project task events are shown on the calendar.</summary>
        [ObservableProperty]
        private bool _showTasks = true;

        /// <summary>When <c>true</c>, SA public holidays are shown on the calendar.</summary>
        [ObservableProperty]
        private bool _showPublicHolidays = true;

        /// <summary>When <c>true</c>, employee birthday events are shown on the calendar.</summary>
        [ObservableProperty]
        private bool _showBirthdays = true;

        /// <summary>When <c>true</c>, approved leave/absence events are shown on the calendar.</summary>
        [ObservableProperty]
        private bool _showLeave = true;

        #endregion

        #region Project Filter Properties

        /// <summary>
        /// All active projects available for filtering.
        /// Populated on load and used as the ItemsSource for the sidebar project list.
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<ProjectFilterItem> _availableProjects = new();

        /// <summary>
        /// Returns the IDs of projects the user has checked in the sidebar filter.
        /// An empty collection means "show all projects".
        /// </summary>
        public IEnumerable<Guid> SelectedProjectIds
            => AvailableProjects.Where(p => p.IsSelected).Select(p => p.Id);

        #endregion

        #region Busy / State Properties

        /// <summary><c>true</c> while the calendar data is being fetched/regenerated.</summary>
        [ObservableProperty]
        private bool _isRefreshing;

        #endregion

        #region Constructor

        /// <summary>
        /// Initialises the CalendarHubViewModel and triggers the first calendar load.
        /// </summary>
        /// <param name="calendarService">Aggregated event service.</param>
        /// <param name="projectService">Used to populate the project filter sidebar.</param>
        public CalendarHubViewModel(
            ICalendarService calendarService,
            IProjectService  projectService)
        {
            _calendarService = calendarService;
            _projectService  = projectService;

            Title        = "Calendar";
            CurrentMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

            // Subscribe to real-time task updates so the grid stays current
            // without requiring a manual refresh from the user.
            WeakReferenceMessenger.Default.Register(this);

            // Load projects for the sidebar filter, then generate the calendar
            _ = InitialiseAsync();
        }

        #endregion

        #region Initialisation

        /// <summary>
        /// Loads the project list for the sidebar filter, then generates the initial calendar.
        /// Fire-and-forget from the constructor — errors are surfaced via IsBusy/ErrorMessage.
        /// </summary>
        private async Task InitialiseAsync()
        {
            try
            {
                IsRefreshing = true;

                // Populate project list for the optional sidebar filter (checked by default)
                var summaries = await _projectService.GetProjectSummariesAsync();
                App.Current.Dispatcher.Invoke(() =>
                {
                    AvailableProjects.Clear();
                    foreach (var p in summaries.OrderBy(s => s.Name))
                        AvailableProjects.Add(new ProjectFilterItem(p.Id, p.Name) { IsSelected = true });
                });

                await GenerateCalendarAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Could not load calendar: {ex.Message}";
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        #endregion

        #region Commands — Month Navigation

        /// <summary>Moves the calendar view forward by one month.</summary>
        [RelayCommand]
        private async Task NextMonth()
        {
            CurrentMonth = CurrentMonth.AddMonths(1);
            await GenerateCalendarAsync();
        }

        /// <summary>Moves the calendar view backward by one month.</summary>
        [RelayCommand]
        private async Task PreviousMonth()
        {
            CurrentMonth = CurrentMonth.AddMonths(-1);
            await GenerateCalendarAsync();
        }

        /// <summary>Jumps the calendar view to the current month.</summary>
        [RelayCommand]
        private async Task GoToToday()
        {
            CurrentMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            if (SelectedDay != null)
            {
                SelectedDay.IsSelected = false;
                SelectedDay = null;
            }
            await GenerateCalendarAsync();
        }

        #endregion

        #region Commands — Day Selection

        /// <summary>
        /// Marks the clicked day cell as selected, deselecting any previous selection.
        /// </summary>
        [RelayCommand]
        private void SelectDay(CalendarDayViewModel day)
        {
            if (day == null) return;

            if (SelectedDay != null)
                SelectedDay.IsSelected = false;

            SelectedDay           = day;
            SelectedDay.IsSelected = true;
        }

        #endregion

        #region Commands — Project Filter

        /// <summary>
        /// Called when the user toggles a project checkbox in the sidebar.
        /// Regenerates the calendar to apply the updated project filter.
        /// </summary>
        [RelayCommand]
        private async Task ToggleProjectFilter()
        {
            await GenerateCalendarAsync();
        }

        /// <summary>Clears all project filter selections (hides all project tasks).</summary>
        [RelayCommand]
        private async Task ClearProjectFilter()
        {
            foreach (var p in AvailableProjects)
                p.IsSelected = false;

            await GenerateCalendarAsync();
        }

        #endregion

        #region Commands — Filter Toggles

        /// <summary>Refreshes the calendar whenever a filter toggle changes.</summary>
        partial void OnShowTasksChanged(bool value)          => _ = GenerateCalendarAsync();
        partial void OnShowPublicHolidaysChanged(bool value) => _ = GenerateCalendarAsync();
        partial void OnShowBirthdaysChanged(bool value)      => _ = GenerateCalendarAsync();
        partial void OnShowLeaveChanged(bool value)          => _ = GenerateCalendarAsync();

        #endregion

        #region Message Handling

        /// <summary>
        /// Handles <see cref="TaskUpdatedMessage"/> to keep the calendar in sync
        /// with task changes made from other parts of the application (e.g. the
        /// project task list or Gantt chart).
        /// </summary>
        public async void Receive(TaskUpdatedMessage message)
        {
            // Regenerate the calendar on the UI thread when a task changes
            await App.Current.Dispatcher.InvokeAsync(async () =>
            {
                await GenerateCalendarAsync();
            });
        }

        #endregion

        #region Calendar Generation

        /// <summary>
        /// Builds the 42-cell day grid for the current month, fetches events from
        /// the calendar service, applies visibility filters, assigns span metadata,
        /// and pushes results to the observable <see cref="Days"/> collection.
        /// </summary>
        private async Task GenerateCalendarAsync()
        {
            try
            {
                IsRefreshing = true;

                // ── Step 1: Update header labels ──────────────────────────────
                MonthName = CurrentMonth.ToString("MMMM");
                YearName  = CurrentMonth.ToString("yyyy");

                // ── Step 2: Build the 42-cell day list ────────────────────────
                _dayList = BuildDayList();

                // ── Step 3: Fetch events from the service ─────────────────────
                var windowStart = _dayList.First().Date;
                var windowEnd   = _dayList.Last().Date;

                var selectedProjects = SelectedProjectIds.ToList();
                var allEvents = await _calendarService.GetEventsAsync(
                    windowStart,
                    windowEnd,
                    AvailableProjects.Any() ? selectedProjects : null);

                // ── Step 4: Apply visibility filters ──────────────────────────
                var filteredEvents = allEvents.Where(e =>
                    (e.Type == CalendarEventType.Task          && ShowTasks)          ||
                    (e.Type == CalendarEventType.PublicHoliday && ShowPublicHolidays) ||
                    (e.Type == CalendarEventType.Birthday       && ShowBirthdays)      ||
                    (e.Type == CalendarEventType.Leave          && ShowLeave)
                ).ToList();

                // ── Step 5: Assign span metadata and populate day cells ────────
                // For each multi-day event, a clone is added to every day it spans,
                // with the appropriate Start/Middle/End span value for corner rounding.
                foreach (var evt in filteredEvents)
                {
                    foreach (var day in _dayList)
                    {
                        if (day.Date.Date < evt.StartDate.Date || day.Date.Date > evt.EndDate.Date)
                            continue;

                        bool isStart = day.Date.Date == evt.StartDate.Date;
                        bool isEnd   = day.Date.Date == evt.EndDate.Date;

                        // Clone the event so each day cell gets its own Span value
                        var spanEvent = CloneWithSpan(evt, isStart, isEnd);
                        day.Events.Add(spanEvent);

                        // Mark the cell as a holiday so the header renders in red
                        if (evt.Type == CalendarEventType.PublicHoliday)
                        {
                            day.IsHoliday  = true;
                            day.HolidayName = evt.Title;
                        }
                    }
                }

                // Recompute the capped visible/overflow lists for each day
                foreach (var day in _dayList)
                    day.RefreshVisibleEvents();

                // ── Step 6: Restore or set selection ──────────────────────────
                var previouslySelected = SelectedDay?.Date;
                CalendarDayViewModel? newSelection = null;

                if (previouslySelected.HasValue)
                {
                    // Try to keep the same day selected after a refresh
                    newSelection = _dayList.FirstOrDefault(d => d.Date.Date == previouslySelected.Value.Date);
                }

                // Fall back to today, or the first day of the current month
                newSelection ??= _dayList.FirstOrDefault(d => d.IsToday)
                              ?? _dayList.FirstOrDefault(d => d.IsCurrentMonth);

                // ── Step 7: Push to observable collection on the UI thread ─────
                App.Current.Dispatcher.Invoke(() =>
                {
                    Days.Clear();
                    foreach (var d in _dayList)
                        Days.Add(d);

                    if (newSelection != null)
                    {
                        if (SelectedDay != null) SelectedDay.IsSelected = false;
                        SelectedDay           = newSelection;
                        SelectedDay.IsSelected = true;
                    }
                });
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Calendar refresh failed: {ex.Message}";
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Constructs the 42-cell list covering the full 6-week display window.
        /// Padding cells from the previous and next months are added so the
        /// grid always starts on Monday and has 42 cells total.
        /// </summary>
        private List<CalendarDayViewModel> BuildDayList()
        {
            var list = new List<CalendarDayViewModel>(42);

            var firstOfMonth  = new DateTime(CurrentMonth.Year, CurrentMonth.Month, 1);
            int daysInMonth   = DateTime.DaysInMonth(CurrentMonth.Year, CurrentMonth.Month);

            // Calculate how many padding days are needed before the 1st.
            // The grid starts on Monday (ISO week), so Sunday = 6 padding days.
            int offset = (int)firstOfMonth.DayOfWeek - 1;
            if (offset < 0) offset = 6; // Sunday wraps to 6

            // ── Previous-month padding ────────────────────────────────────────
            var prevMonth      = CurrentMonth.AddMonths(-1);
            int daysInPrevMonth = DateTime.DaysInMonth(prevMonth.Year, prevMonth.Month);
            for (int i = 0; i < offset; i++)
            {
                int day = daysInPrevMonth - offset + 1 + i;
                list.Add(new CalendarDayViewModel(
                    new DateTime(prevMonth.Year, prevMonth.Month, day), isCurrentMonth: false));
            }

            // ── Current month ─────────────────────────────────────────────────
            for (int i = 1; i <= daysInMonth; i++)
                list.Add(new CalendarDayViewModel(
                    new DateTime(CurrentMonth.Year, CurrentMonth.Month, i), isCurrentMonth: true));

            // ── Next-month padding (fill to exactly 42 cells) ─────────────────
            var nextMonth = CurrentMonth.AddMonths(1);
            int remaining = 42 - list.Count;
            for (int i = 1; i <= remaining; i++)
                list.Add(new CalendarDayViewModel(
                    new DateTime(nextMonth.Year, nextMonth.Month, i), isCurrentMonth: false));

            return list;
        }

        /// <summary>
        /// Returns a shallow copy of <paramref name="source"/> with the correct
        /// <see cref="CalendarEvent.Span"/> value for its position in a multi-day event.
        /// </summary>
        private static CalendarEvent CloneWithSpan(CalendarEvent source, bool isStart, bool isEnd)
        {
            var span = (isStart && isEnd) ? CalendarEventSpan.Single
                     : isStart            ? CalendarEventSpan.Start
                     : isEnd              ? CalendarEventSpan.End
                                          : CalendarEventSpan.Middle;

            return new CalendarEvent
            {
                Id             = source.Id,
                Type           = source.Type,
                Title          = source.Title,
                Description    = source.Description,
                StartDate      = source.StartDate,
                EndDate        = source.EndDate,
                Color          = source.Color,
                IsCompleted    = source.IsCompleted,
                ProjectName    = source.ProjectName,
                Span           = span,
                OriginalSource = source.OriginalSource
            };
        }

        #endregion

        #region Dispose

        /// <inheritdoc/>
        public override void Dispose()
        {
            // Unregister all WeakReferenceMessenger subscriptions to prevent memory leaks
            WeakReferenceMessenger.Default.UnregisterAll(this);
            base.Dispose();
        }

        #endregion
    }

    // =========================================================================
    // ProjectFilterItem
    // Lightweight model for sidebar project checkboxes.
    // =========================================================================

    /// <summary>
    /// Represents a single project in the calendar sidebar filter list.
    /// </summary>
    public partial class ProjectFilterItem : ObservableObject
    {
        /// <summary>The unique identifier of the project.</summary>
        public Guid Id { get; }

        /// <summary>The display name of the project shown in the checkbox label.</summary>
        public string Name { get; }

        /// <summary><c>true</c> when this project's tasks should appear on the calendar.</summary>
        [ObservableProperty]
        private bool _isSelected;

        /// <summary>Initialises a new project filter item.</summary>
        public ProjectFilterItem(Guid id, string name)
        {
            Id   = id;
            Name = name;
        }
    }
}
