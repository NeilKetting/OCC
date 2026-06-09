using Microsoft.Extensions.DependencyInjection;
using OCC.WpfClient.Features.CalendarHub.Services;
using OCC.WpfClient.Features.CalendarHub.ViewModels;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using System.Collections.Generic;

namespace OCC.WpfClient.Features.CalendarHub
{
    // =========================================================================
    // CalendarFeature.cs
    // IFeature registration for the unified Calendar hub.
    // Follows the same pattern as ProjectFeature, AttendanceFeature, etc.
    // =========================================================================

    /// <summary>
    /// Registers all services, ViewModels, and navigation routes for the
    /// Calendar feature.  Exposes a single top-level nav item: "Calendar".
    /// </summary>
    public class CalendarFeature : IFeature
    {
        #region IFeature Metadata

        /// <inheritdoc/>
        public string Name => "Calendar";

        /// <inheritdoc/>
        /// <remarks>
        /// Order 25 places Calendar between Chat (20) and Projects (30)
        /// in the navigation sidebar.
        /// </remarks>
        public int Order => 25;

        #endregion

        #region Service Registration

        /// <inheritdoc/>
        public void RegisterServices(IServiceCollection services)
        {
            // ── Calendar-specific services ────────────────────────────────────
            // Singleton: HolidayService has an in-memory year cache that should
            // survive the lifetime of the app session.
            services.AddSingleton<IHolidayService, HolidayService>();

            // Singleton: CalendarService holds no mutable state (depends on
            // singletons) so it can be safely shared across navigations.
            services.AddSingleton<ICalendarService, CalendarService>();

            // ── ViewModels ────────────────────────────────────────────────────
            // Transient: a fresh ViewModel is created each time the user navigates
            // to the Calendar so the grid reloads with up-to-date data.
            services.AddTransient<CalendarHubViewModel>();
        }

        #endregion

        #region Route Registration

        /// <inheritdoc/>
        public void RegisterRoutes(INavigationService navigationService)
        {
            // Map the existing "Calendar" route constant to CalendarHubViewModel.
            // The DataTemplate in FeatureTemplates.xaml handles VM → View resolution.
            navigationService.RegisterRoute(NavigationRoutes.Calendar, typeof(CalendarHubViewModel));
        }

        #endregion

        #region Navigation Items

        /// <inheritdoc/>
        public IEnumerable<NavItem> GetNavigationItems()
        {
            // Single top-level Calendar item — no sub-items needed at this stage.
            // The calendar icon (&#xE787;) is the Segoe MDL2 "Calendar" glyph.
            yield return new NavItem(
                label:     "Calendar",
                route:     NavigationRoutes.Calendar,
                category:  "Workspace",
                iconColor: "#C084FC",
                iconCode:  "\uE787");
        }

        #endregion
    }
}
