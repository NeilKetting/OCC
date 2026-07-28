using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OCC.WpfClient.Features.CalendarHub.Models;
using OCC.WpfClient.Features.CalendarHub.Services;
using OCC.WpfClient.Infrastructure.Messages;
using OCC.Shared.Models;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.WpfClient.Features.MainHub.ViewModels
{
    public partial class CalendarWidgetViewModel : WidgetViewModelBase
    {
        private readonly ICalendarService _calendarService;

        [ObservableProperty]
        private bool _isLoadingEvents;

        public ObservableCollection<CalendarEvent> UpcomingEvents { get; } = new();

        public CalendarWidgetViewModel(ICalendarService calendarService)
        {
            _calendarService = calendarService;
            WidgetId = "Calendar";
            Title = "Upcoming Schedule";
        }

        [RelayCommand]
        private void NavigateToEvent(CalendarEvent ev)
        {
            if (ev == null) return;
            if (ev.Type == CalendarEventType.Task && ev.OriginalSource is ProjectTask task)
            {
                if (task.ProjectId.HasValue)
                {
                    WeakReferenceMessenger.Default.Send(new OpenProjectTaskMessage(task.ProjectId.Value, task.Id));
                }
            }
            else
            {
                WeakReferenceMessenger.Default.Send(new OpenHubMessage("Calendar"));
            }
        }

        [RelayCommand]
        private void NavigateToCalendar()
        {
            WeakReferenceMessenger.Default.Send(new OpenHubMessage("Calendar"));
        }

        public override async Task RefreshDataAsync()
        {
            if (IsLoadingEvents) return;
            IsLoadingEvents = true;
            try
            {
                var today = System.DateTime.Today;
                var endOfNextWeek = today.AddDays(7);
                var events = await _calendarService.GetEventsAsync(today, endOfNextWeek);
                var sortedEvents = events.OrderBy(e => e.StartDate).ThenBy(e => e.Title).ToList();
                App.Current.Dispatcher.Invoke(() =>
                {
                    UpcomingEvents.Clear();
                    foreach (var ev in sortedEvents)
                    {
                        UpcomingEvents.Add(ev);
                    }
                });
            }
            catch { }
            finally
            {
                IsLoadingEvents = false;
            }
        }
    }
}
