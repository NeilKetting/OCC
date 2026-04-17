using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using OCC.Shared.Interfaces;
using OCC.Shared.Services;
using OCC.Client.Services;
using OCC.Client.Services.Infrastructure;
using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Managers;
using OCC.Client.Services.Managers.Interfaces;
using OCC.Client.Services.Repositories.ApiServices;
using OCC.Client.Services.Repositories.Interfaces;
using OCC.Client.ViewModels.Core;
using OCC.Client.Features.EmployeeHub.ViewModels;
using OCC.Client.Features.TimeAttendanceHub.ViewModels;
using OCC.Client.Features.HseqHub.ViewModels;
using OCC.Client.Features.HomeHub.ViewModels;
using OCC.Client.Features.HomeHub.ViewModels.Dashboard;
using OCC.Client.Features.HomeHub.ViewModels.Shared;
using OCC.Client.Features.AuthHub.ViewModels;
using OCC.Client.Features.SettingsHub.ViewModels;
using OCC.Client.Features.BugHub.ViewModels;
using OCC.Client.Features.CustomerHub.ViewModels;
using OCC.Client.ViewModels.Notifications;
using OCC.Client.Features.OrdersHub.ViewModels;
using OCC.Client.Features.OrdersHub.UseCases;
using OCC.Client.Features.ProjectsHub.ViewModels;
using OCC.Client.Features.TaskHub.ViewModels;
using OCC.Client.Features.CalendarHub.ViewModels;
using OCC.Client.ViewModels.Shared;
using OCC.Client.Views.Core;
using OCC.Shared.Models;
using OCC.Client.Features.CoreHub.ViewModels;
using OCC.Client.Mobile.Shell;
using OCC.Client.Mobile.Shell.BottomNav;
using OCC.Client.Mobile.Features.Dashboard;
using OCC.Client.Mobile.Features.Auth.Login;
using OCC.Client.Mobile.Features.RollCall;
using OCC.Client.Features.WagesHub.ViewModels;

namespace OCC.Client.Infrastructure.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
        {
            // --- Infrastructure & Core Services ---
            services.AddTransient<FailureLoggingHandler>();
            services.AddSingleton(ConnectionSettings.Instance);
            services.AddSingleton<ITimeService, TimeService>();
            services.AddSingleton<ITimeServiceV2, TimeServiceV2>();

            services.AddSingleton<IUpdateService, UpdateService>();
            services.AddSingleton<IPdfService, PdfService>();
            services.AddSingleton<IExportService, ExportService>();
            services.AddSingleton<IDialogService, DialogService>();
            services.AddSingleton<IToastService, ToastService>();
            services.AddSingleton<UserActivityService>();
            services.AddSingleton<SignalRNotificationService>();
            services.AddSingleton<IPermissionService, PermissionService>();
            services.AddSingleton<LocalSettingsService>();
            services.AddSingleton<UserPreferencesService>();
            services.AddHttpClient("GoogleMaps", c => { })
                .AddHttpMessageHandler<FailureLoggingHandler>();
            services.AddTransient<IGoogleMapsService>(sp => 
            {
                var factory = sp.GetRequiredService<IHttpClientFactory>();
                var client = factory.CreateClient("GoogleMaps");
                return new GoogleMapsService(client, ConnectionSettings.Instance.GoogleApiKey);
            });
            services.AddSingleton<ILogUploadService, LogUploadService>();

            // --- Database & Repositories ---
            services.AddDbContext<Data.AppDbContext>(options => { }, ServiceLifetime.Transient); 
            
            services.AddTransient<IRepository<User>, ApiUserRepository>();
            services.AddHttpClient<IRepository<Employee>, ApiEmployeeRepository>(c => c.BaseAddress = new Uri(ConnectionSettings.Instance.ApiBaseUrl))
                .AddHttpMessageHandler<FailureLoggingHandler>();

            services.AddTransient<IRepository<Project>, ApiProjectRepository>();
            services.AddTransient<IRepository<ProjectTask>, ApiProjectTaskRepository>();
            services.AddTransient<IProjectTaskRepository, ApiProjectTaskRepository>();
            services.AddTransient<IRepository<Customer>, ApiCustomerRepository>();
            services.AddTransient<IRepository<TaskAssignment>, ApiTaskAssignmentRepository>();
            services.AddTransient<IRepository<TaskComment>, ApiTaskCommentRepository>();

            services.AddHttpClient<IRepository<TimeRecord>, ApiTimeRecordRepository>(c => c.BaseAddress = new Uri(ConnectionSettings.Instance.ApiBaseUrl))
                .AddHttpMessageHandler<FailureLoggingHandler>();

            services.AddHttpClient<IRepository<AttendanceRecord>, ApiAttendanceRecordRepository>(c => c.BaseAddress = new Uri(ConnectionSettings.Instance.ApiBaseUrl))
                .AddHttpMessageHandler<FailureLoggingHandler>();

            services.AddTransient<IRepository<AppSetting>, ApiAppSettingRepository>();
            services.AddTransient<IRepository<Team>, ApiTeamRepository>();
            services.AddTransient<IRepository<TeamMember>, ApiTeamMemberRepository>();

            services.AddHttpClient<IRepository<LeaveRequest>, ApiLeaveRequestRepository>(c => c.BaseAddress = new Uri(ConnectionSettings.Instance.ApiBaseUrl))
                .AddHttpMessageHandler<FailureLoggingHandler>();

            services.AddTransient<IRepository<PublicHoliday>, ApiPublicHolidayRepository>();

            services.AddHttpClient<IRepository<OvertimeRequest>, ApiOvertimeRequestRepository>(c => c.BaseAddress = new Uri(ConnectionSettings.Instance.ApiBaseUrl))
                .AddHttpMessageHandler<FailureLoggingHandler>();

            return services;
        }

        public static IServiceCollection AddAuthHub(this IServiceCollection services)
        {
            // Core Hub (Shell & Main)
            services.AddTransient<ShellViewModel>();
            services.AddTransient<MainViewModel>();
            services.AddSingleton<SideMenuViewModel>();
            services.AddTransient<AccessDeniedViewModel>();

            // Auth Hub
            services.AddSingleton<ApiAuthService>();
            services.AddSingleton<IAuthService>(sp => sp.GetRequiredService<ApiAuthService>());
            services.AddTransient<LoginViewModel>();
            services.AddTransient<RegisterViewModel>();

            return services;
        }

        public static IServiceCollection AddHomeHub(this IServiceCollection services)
        {
            services.AddTransient<HomeViewModel>();
            services.AddTransient<HomeMenuViewModel>();
            services.AddTransient<SummaryViewModel>();
            services.AddTransient<TasksWidgetViewModel>();
            services.AddTransient<PulseViewModel>();
            services.AddSingleton<NotificationViewModel>();
            services.AddSingleton<INotificationService, ApiNotificationService>();

            return services;
        }

        public static IServiceCollection AddProjectsHub(this IServiceCollection services)
        {
            services.AddSingleton<IProjectManager, ProjectManager>();
            services.AddTransient<ProjectsViewModel>();
            services.AddTransient<ProjectMainMenuViewModel>();
            services.AddTransient<ProjectListViewModel>();
            services.AddTransient<ProjectDetailViewModel>();
            services.AddTransient<CreateProjectViewModel>();
            services.AddTransient<EditProjectViewModel>();
            services.AddTransient<ProjectReportViewModel>();
            services.AddTransient<ProjectCustomerReportViewModel>();
            services.AddTransient<ProjectTopBarViewModel>();
            services.AddTransient<ProjectGanttViewModel>();
            services.AddTransient<ProjectVariationOrderListViewModel>();
            services.AddTransient<ProjectFilesViewModel>();
            services.AddHttpClient<IProjectService, ProjectService>(client => client.BaseAddress = new Uri(ConnectionSettings.Instance.ApiBaseUrl))
                .AddHttpMessageHandler<FailureLoggingHandler>();
            services.AddHttpClient<IProjectVariationOrderService, ProjectVariationOrderService>(client => client.BaseAddress = new Uri(ConnectionSettings.Instance.ApiBaseUrl))
                .AddHttpMessageHandler<FailureLoggingHandler>();
            
            services.AddTransient<TaskListViewModel>();
            services.AddTransient<TaskDetailViewModel>();
            services.AddHttpClient<ITaskAttachmentService, ApiTaskAttachmentService>(client => client.BaseAddress = new Uri(ConnectionSettings.Instance.ApiBaseUrl))
                .AddHttpMessageHandler<FailureLoggingHandler>();

            // Calendar Hub
            services.AddTransient<ICalendarService, CalendarService>();
            services.AddTransient<CalendarHubViewModel>();

            return services;
        }

        public static IServiceCollection AddEmployeeHub(this IServiceCollection services)
        {
            services.AddHttpClient<IEmployeeService, ApiEmployeeService>(client => client.BaseAddress = new Uri(ConnectionSettings.Instance.ApiBaseUrl))
                .AddHttpMessageHandler<FailureLoggingHandler>();
            services.AddTransient<EmployeeManagementViewModel>();
            services.AddTransient<EmployeeDetailViewModel>();
            services.AddSingleton<IEmployeeImportService, EmployeeImportService>();
            
            services.AddTransient<TeamManagementViewModel>();
            services.AddTransient<TeamDetailViewModel>();
            
            services.AddTransient<TimeAttendanceViewModel>();
            services.AddTransient<TimeLiveViewModel>();
            services.AddTransient<TimeLiveV2ViewModel>();
            services.AddTransient<TimeMenuViewModel>();
            services.AddTransient<DailyTimesheetViewModel>();
            services.AddTransient<DailyTimesheetV2ViewModel>();
            services.AddTransient<AttendanceHistoryViewModel>();
            services.AddTransient<ManualAttendanceViewModel>();

            services.AddTransient<ILeaveService, LeaveService>();
            services.AddTransient<LeaveApplicationViewModel>();
            services.AddTransient<LeaveApprovalViewModel>();
            
            services.AddTransient<IHolidayService, HolidayService>();
            services.AddTransient<OvertimeViewModel>();
            services.AddTransient<OvertimeApprovalViewModel>();
            
            services.AddSingleton<IReminderService, ReminderService>();

            return services;
        }

        public static IServiceCollection AddOrdersHub(this IServiceCollection services)
        {
            // Customer Hub
            services.AddHttpClient<ICustomerService, CustomerService>(client => client.BaseAddress = new Uri(ConnectionSettings.Instance.ApiBaseUrl))
                .AddHttpMessageHandler<FailureLoggingHandler>();
            services.AddTransient<CustomerManagementViewModel>();
            services.AddTransient<CustomerDetailViewModel>();

            // Orders Hub
            services.AddSingleton<OrderStateService>();
            services.AddTransient<IOrderLifecycleService, OrderLifecycleService>();
            services.AddTransient<IOrderManager, OrderManager>();
            services.AddHttpClient<IOrderService, OrderService>(client => client.BaseAddress = new Uri(ConnectionSettings.Instance.ApiBaseUrl))
                .AddHttpMessageHandler<FailureLoggingHandler>();
            services.AddHttpClient<IInventoryService, InventoryService>(client => client.BaseAddress = new Uri(ConnectionSettings.Instance.ApiBaseUrl))
                .AddHttpMessageHandler<FailureLoggingHandler>();
            services.AddHttpClient<ISupplierService, SupplierService>(client => client.BaseAddress = new Uri(ConnectionSettings.Instance.ApiBaseUrl))
                .AddHttpMessageHandler<FailureLoggingHandler>();
            services.AddSingleton<IInventoryImportService, InventoryImportService>();
            services.AddSingleton<ISupplierImportService, SupplierImportService>();
            
            services.AddTransient<OrderViewModel>();
            services.AddTransient<OrderMenuViewModel>();
            services.AddTransient<OrderDashboardViewModel>();
            services.AddTransient<OrderListViewModel>();
            services.AddSingleton<IOrderCalculationService, OrderCalculationService>();
            services.AddTransient<OrderLinesViewModel>();
            services.AddTransient<InventoryLookupViewModel>();
            services.AddTransient<SupplierSelectorViewModel>();
            services.AddTransient<OrderSubmissionUseCase>();
            services.AddTransient<CreateOrderViewModel>();
            services.AddTransient<PickingOrderViewModel>();
            services.AddTransient<ReceiveOrderViewModel>();
            services.AddTransient<InventoryViewModel>();
            services.AddTransient<ItemDetailViewModel>();
            services.AddTransient<SupplierListViewModel>();
            services.AddTransient<SupplierDetailViewModel>();
            services.AddTransient<RestockReviewViewModel>();

            return services;
        }

        public static IServiceCollection AddHseqHub(this IServiceCollection services)
        {
            services.AddHttpClient<IHealthSafetyService, ApiHealthSafetyService>(client => client.BaseAddress = new Uri(ConnectionSettings.Instance.ApiBaseUrl))
                .AddHttpMessageHandler<FailureLoggingHandler>();
            services.AddTransient<HealthSafetyViewModel>();
            services.AddTransient<HealthSafetyDashboardViewModel>();
            services.AddTransient<HealthSafetyMenuViewModel>();
            services.AddTransient<PerformanceMonitoringViewModel>();
            services.AddTransient<IncidentsViewModel>();
            services.AddTransient<IncidentEditorViewModel>();
            services.AddTransient<TrainingViewModel>();
            services.AddTransient<TrainingEditorViewModel>();
            services.AddTransient<AuditsViewModel>();
            services.AddTransient<AuditEditorViewModel>();
            services.AddTransient<AuditDeviationsViewModel>();
            services.AddTransient<DocumentsViewModel>();

            return services;
        }

        public static IServiceCollection AddSupportHub(this IServiceCollection services)
        {
            services.AddHttpClient<IBugReportService, BugReportService>(client => client.BaseAddress = new Uri(ConnectionSettings.Instance.ApiBaseUrl))
                .AddHttpMessageHandler<FailureLoggingHandler>();
            services.AddTransient<BugListViewModel>();
            services.AddTransient<SupportCenterViewModel>();

            return services;
        }

        public static IServiceCollection AddSettingsHub(this IServiceCollection services)
        {
            services.AddHttpClient<ISettingsService, SettingsService>(client => client.BaseAddress = new Uri(ConnectionSettings.Instance.ApiBaseUrl))
                .AddHttpMessageHandler<FailureLoggingHandler>();
            services.AddSingleton<IAuditLogService, ApiAuditLogService>();
            services.AddTransient<AuditLogViewModel>();
            services.AddTransient<CompanyProfileViewModel>();
            services.AddTransient<CompanySettingsViewModel>();
            services.AddTransient<UserManagementViewModel>();
            services.AddTransient<ManageUsersViewModel>();
            services.AddTransient<UserPreferencesViewModel>();
            services.AddTransient<ProfileViewModel>();
            services.AddTransient<OCC.Client.ViewModels.Developer.DeveloperViewModel>();
            
            return services;
        }

        public static IServiceCollection AddMobileHub(this IServiceCollection services)
        {
            services.AddTransient<MobileDashboardViewModel>();
            services.AddTransient<MobileRollCallViewModel>();
            services.AddTransient<MobileHubViewModel>();
            services.AddTransient<MobileLoginViewModel>();
            services.AddTransient<SiteManagerDashboardViewModel>();
            
            return services;
        }

        public static IServiceCollection AddWagesHub(this IServiceCollection services)
        {
            services.AddTransient<IWageService, WageService>();
            services.AddTransient<WageRunViewModel>();

            services.AddHttpClient<IEmployeeLoanService, ApiEmployeeLoanService>(client => client.BaseAddress = new Uri(ConnectionSettings.Instance.ApiBaseUrl))
                .AddHttpMessageHandler<FailureLoggingHandler>();
            services.AddTransient<LoansManagementViewModel>();
            services.AddTransient<AddLoanDialogViewModel>();

            services.AddTransient<WagesViewModel>();
            services.AddTransient<WagesMenuViewModel>();

            return services;
		}
    }
}
