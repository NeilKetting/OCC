using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using OCC.Client.ViewModels.Core;
using System;
using Avalonia;
using OCC.Client.Mobile.Features.Dashboard;
using OCC.Client.Mobile.Features.RollCall;
using OCC.Client;

namespace OCC.Client.Mobile.Shell
{
    public partial class MobileShellViewModelBase : ViewModelBase, IRecipient<OpenProjectMessage>, IRecipient<NavigateBackMessage>
    {
        [ObservableProperty]
        private ViewModelBase _currentView;

        private readonly SiteManagerDashboardViewModel _dashboardViewModel;
        private readonly MobileRollCallViewModel _rollCallViewModel;

        public MobileShellViewModelBase(
            SiteManagerDashboardViewModel dashboardViewModel,
            MobileRollCallViewModel rollCallViewModel)
        {
            _dashboardViewModel = dashboardViewModel;
            _rollCallViewModel = rollCallViewModel;
            _currentView = _dashboardViewModel;

            WeakReferenceMessenger.Default.Register<OpenProjectMessage>(this);
            WeakReferenceMessenger.Default.Register<NavigateBackMessage>(this);
        }

        [RelayCommand]
        private void NavigateToDashboard() => CurrentView = _dashboardViewModel;

        [RelayCommand]
        private void NavigateToRollCall() => CurrentView = _rollCallViewModel;

        public void Receive(OpenProjectMessage message)
        {
            var services = ((App)Avalonia.Application.Current!).Services;
            if (services != null)
            {
                var projectService = services.GetRequiredService<OCC.Client.Services.Interfaces.IProjectService>();
                var taskRepo = services.GetRequiredService<OCC.Client.Services.Repositories.Interfaces.IProjectTaskRepository>();
                var commentRepo = services.GetRequiredService<OCC.Client.Services.Repositories.Interfaces.IRepository<OCC.Shared.Models.TaskComment>>();
                var attachService = services.GetRequiredService<OCC.Client.Services.Interfaces.ITaskAttachmentService>();
                
                var vm = new MobileProjectDetailViewModel(message.ProjectId, projectService, taskRepo, commentRepo, attachService);
                _ = vm.InitializeAsync();
                CurrentView = vm;
            }
        }

        public void Receive(NavigateBackMessage message)
        {
            CurrentView = _dashboardViewModel;
        }
    }
}
