using Microsoft.Extensions.DependencyInjection;
using Moq;
using OCC.Client;
using OCC.WpfClient.Features.AuthHub.ViewModels;
using OCC.WpfClient.Features.EmployeeHub.ViewModels;
using OCC.WpfClient.Features.HseqHub.ViewModels;
using OCC.WpfClient.Features.OrdersHub.ViewModels;
using OCC.WpfClient.Features.TimeAttendanceHub.ViewModels;
using OCC.WpfClient.Services.Interfaces;
using System;
using Xunit;

namespace OCC.Tests.Infrastructure
{
    public class DependencyInjectionTests
    {
        private readonly IServiceProvider _serviceProvider;

        public DependencyInjectionTests()
        {
            var services = new ServiceCollection();
            
            // We use the real ConfigureServices to ensure we are testing the actual registrations
            var app = new App();
            app.ConfigureServices(services);

            // Overwrite some problematic services with mocks if they cause resolution issues 
            // (e.g. services that require specific runtime state in their constructor)
            // For now, we try resolving with the real setup.

            _serviceProvider = services.BuildServiceProvider();
        }

        [Theory]
        [InlineData(typeof(LoginViewModel))]
        [InlineData(typeof(AuditsViewModel))]
        [InlineData(typeof(AuditEditorViewModel))]
        [InlineData(typeof(AuditDeviationsViewModel))]
        [InlineData(typeof(IncidentsViewModel))]
        [InlineData(typeof(IncidentEditorViewModel))]
        [InlineData(typeof(TrainingViewModel))]
        [InlineData(typeof(TrainingEditorViewModel))]
        [InlineData(typeof(EmployeeManagementViewModel))]
        [InlineData(typeof(EmployeeDetailViewModel))]
        [InlineData(typeof(CreateOrderViewModel))]
        [InlineData(typeof(OrderLinesViewModel))]
        [InlineData(typeof(InventoryLookupViewModel))]
        [InlineData(typeof(SupplierSelectorViewModel))]
        [InlineData(typeof(DailyTimesheetViewModel))]
        [InlineData(typeof(HealthSafetyDashboardViewModel))]
        [InlineData(typeof(PerformanceMonitoringViewModel))]
        public void ViewModel_ShouldBeResolvable(Type viewModelType)
        {
            // Act
            var viewModel = _serviceProvider.GetService(viewModelType);

            // Assert
            Assert.NotNull(viewModel);
        }

        [Fact]
        public void AllHseqViewModels_ShouldBeResolvable()
        {
            // Specifically check the Hub that the user is worried about
            Assert.NotNull(_serviceProvider.GetService<AuditsViewModel>());
            Assert.NotNull(_serviceProvider.GetService<IncidentsViewModel>());
            Assert.NotNull(_serviceProvider.GetService<TrainingViewModel>());
            Assert.NotNull(_serviceProvider.GetService<PerformanceMonitoringViewModel>());
        }
    }
}
