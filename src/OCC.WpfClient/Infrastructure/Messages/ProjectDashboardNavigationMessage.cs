using System;

namespace OCC.WpfClient.Infrastructure.Messages
{
    /// <summary>
    /// Message sent from the project-specific dashboard to request navigation to a sub-view.
    /// </summary>
    public class ProjectDashboardNavigationMessage
    {
        public string TargetView { get; } // "Tasks", "Safety", "VariationOrders"
        public string Filter { get; } // "All", "Completed", "InProgress", "Overdue"

        public ProjectDashboardNavigationMessage(string targetView, string filter = "All")
        {
            TargetView = targetView;
            Filter = filter;
        }
    }
}
