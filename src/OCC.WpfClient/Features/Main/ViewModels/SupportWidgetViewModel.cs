using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.WpfClient.Services.Interfaces;
using OCC.Shared.Models;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.WpfClient.Features.Main.ViewModels
{
    public partial class SupportWidgetViewModel : WidgetViewModelBase
    {
        private readonly IBugReportService _bugService;
        private readonly IAuthService _authService;
        private readonly IPermissionService _permissionService;

        [ObservableProperty]
        private bool _isLoadingSupportTickets;

        public ObservableCollection<BugReport> OpenSupportTickets { get; } = new();
        public ObservableCollection<BugReport> SupportTicketsNeedingFeedback { get; } = new();

        public SupportWidgetViewModel(IBugReportService bugService, IAuthService authService, IPermissionService permissionService)
        {
            _bugService = bugService;
            _authService = authService;
            _permissionService = permissionService;
            WidgetId = "Support";
            Title = "Support Hub";
        }

        public override async Task RefreshDataAsync()
        {
            if (IsLoadingSupportTickets) return;
            IsLoadingSupportTickets = true;
            try
            {
                var bugs = await _bugService.GetBugReportsAsync(includeArchived: false);
                var bugList = bugs.ToList();

                var currentUserId = _authService.CurrentUser?.Id;
                var isDevOrAdmin = _permissionService.IsDev || _authService.CurrentUser?.UserRole == UserRole.Admin;

                var openBugs = new System.Collections.Generic.List<BugReport>();
                var needingFeedback = new System.Collections.Generic.List<BugReport>();

                if (isDevOrAdmin)
                {
                    openBugs = bugList.Where(b => b.Status != "Closed" && b.Status != "Resolved").ToList();
                }
                else
                {
                    var myBugs = bugList.Where(b => b.ReporterId == currentUserId && b.Status != "Closed" && b.Status != "Resolved").ToList();
                    openBugs = myBugs;

                    foreach (var bugSummary in myBugs)
                    {
                        var fullBug = await _bugService.GetBugReportAsync(bugSummary.Id);
                        if (fullBug != null)
                        {
                            var lastComment = fullBug.Comments.OrderBy(c => c.CreatedAtUtc).LastOrDefault();
                            bool lastCommentIsFromDev = lastComment != null && (lastComment.IsDevComment || lastComment.AuthorEmail != _authService.CurrentUser?.Email);

                            if (fullBug.Status == "Waiting for Client" || lastCommentIsFromDev)
                            {
                                needingFeedback.Add(fullBug);
                            }
                        }
                    }
                }

                App.Current.Dispatcher.Invoke(() =>
                {
                    OpenSupportTickets.Clear();
                    foreach (var bug in openBugs.Take(5))
                    {
                        OpenSupportTickets.Add(bug);
                    }

                    SupportTicketsNeedingFeedback.Clear();
                    foreach (var bug in needingFeedback)
                    {
                        SupportTicketsNeedingFeedback.Add(bug);
                    }
                });
            }
            catch { }
            finally
            {
                IsLoadingSupportTickets = false;
            }
        }
    }
}
