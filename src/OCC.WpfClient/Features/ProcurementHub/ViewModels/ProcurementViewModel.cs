using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Infrastructure.Messages;
using OCC.WpfClient.Services.Interfaces;

namespace OCC.WpfClient.Features.ProcurementHub.ViewModels
{
    public partial class ProcurementViewModel : ViewModelBase
    {
        private readonly ILogger<ProcurementViewModel> _logger;
        private readonly INavigationService _navigationService;

        public ProcurementViewModel(ILogger<ProcurementViewModel> logger, INavigationService navigationService)
        {
            _logger = logger;
            _navigationService = navigationService;
            Title = "Procurement Overview";
            _logger.LogInformation("ProcurementViewModel initialized");
        }

        [RelayCommand]
        private void NavigateToPurchaseOrder()
        {
            WeakReferenceMessenger.Default.Send(new OpenHubMessage(NavigationRoutes.PurchaseOrder));
        }
    }
}
