using Microsoft.Extensions.DependencyInjection;
using OCC.WpfClient.Features.AttendanceHub.ViewModels;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Services;
using OCC.WpfClient.Services.Interfaces;

namespace OCC.WpfClient.Features.AttendanceHub
{
    public class AttendanceFeature : IFeature
    {
        public string Name => "Time & Attendance";
        public int Order => 35; // sits between Employee (30) and HSEQ (40)

        public void RegisterServices(IServiceCollection services)
        {
            services.AddSingleton<IAttendanceService, AttendanceService>();
            services.AddSingleton<ILeaveService, LeaveService>();

            services.AddTransient<AttendanceMenuViewModel>();
            services.AddTransient<AttendanceDashboardViewModel>();
            services.AddTransient<AttendanceHistoryListViewModel>();
            services.AddTransient<TeamManagementViewModel>();
            services.AddTransient<LeaveManagementViewModel>();
            services.AddTransient<AttendanceViewModel>();
        }

        public void RegisterRoutes(INavigationService navigationService)
        {
            navigationService.RegisterRoute(NavigationRoutes.AttendanceLive, typeof(AttendanceViewModel));
        }

        public IEnumerable<NavItem> GetNavigationItems()
        {
            var hub = new NavItem(
                "Time & Attendance",
                NavigationRoutes.AttendanceLive,
                "Operations",
                iconColor: "#00BCD4",
                iconCode: "\uE916");

            yield return hub;
        }
    }
}
