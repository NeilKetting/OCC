using CommunityToolkit.Mvvm.ComponentModel;
using OCC.Mobile.ViewModels;
using OCC.Mobile.Services;
using System.Collections.ObjectModel;

namespace OCC.Mobile.Features.AdminDashboard
{
    public partial class AdminDashboardViewModel : ViewModelBase
    {
        private readonly INavigationService _navigationService;

        [ObservableProperty]
        private string _activeProjectsCount = "3";

        [ObservableProperty]
        private string _overdueTasksCount = "9";

        [ObservableProperty]
        private string _avgCompletion = "50%";

        [ObservableProperty]
        private double _completionProgress = 0.5;

        public ObservableCollection<ActivityItem> RecentUpdates { get; } = new();

        public AdminDashboardViewModel(INavigationService navigationService)
        {
            _navigationService = navigationService;
            Title = "Admin Dashboard";
            LoadMockData();
        }

        private void LoadMockData()
        {
            RecentUpdates.Clear();
            RecentUpdates.Add(new ActivityItem("Task 'Test Sub' progress: Halfway", "updated by Neil Ketting", "2026-04-22 21:07", "#818CF8"));
            RecentUpdates.Add(new ActivityItem("Task 'test' progress: Halfway", "updated by Neil Ketting", "2026-04-21 14:55", "#818CF8"));
            RecentUpdates.Add(new ActivityItem("Task 'Test 3' completed", "by Neil Ketting", "2026-04-20 05:12", "#10B981"));
            RecentUpdates.Add(new ActivityItem("Task 'Sub 2' progress: Almost Done", "updated by Neil Ketting", "2026-04-17 16:30", "#F472B6"));
            RecentUpdates.Add(new ActivityItem("Task 'Test1' progress: Almost Done", "updated by Neil Ketting", "2026-04-17 15:18", "#F472B6"));
        }
    }

    public class ActivityItem
    {
        public string Title { get; }
        public string Subtitle { get; }
        public string Timestamp { get; }
        public string Color { get; }

        public ActivityItem(string title, string subtitle, string timestamp, string color)
        {
            Title = title;
            Subtitle = subtitle;
            Timestamp = timestamp;
            Color = color;
        }
    }
}
