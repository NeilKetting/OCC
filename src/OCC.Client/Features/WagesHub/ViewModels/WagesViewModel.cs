using CommunityToolkit.Mvvm.ComponentModel;
using OCC.Client.ViewModels.Core;
using OCC.Client.Services.Interfaces;

namespace OCC.Client.Features.WagesHub.ViewModels
{
    public partial class WagesViewModel : ViewModelBase
    {
        [ObservableProperty] private WagesMenuViewModel _wagesMenu;
        [ObservableProperty] private ViewModelBase _currentView;

        [ObservableProperty] private WageRunViewModel _wageRunVM;
        [ObservableProperty] private LoansManagementViewModel _loansVM;

        public WagesViewModel(
            WagesMenuViewModel wagesMenu,
            WageRunViewModel wageRunViewModel,
            LoansManagementViewModel loansManagementViewModel)
        {
            _wagesMenu = wagesMenu;
            _wageRunVM = wageRunViewModel;
            _loansVM = loansManagementViewModel;

            _currentView = _wageRunVM;
            _wagesMenu.ActiveTab = "WageRun";
            _wagesMenu.PropertyChanged += WagesMenu_PropertyChanged;
        }

        private void WagesMenu_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(WagesMenuViewModel.ActiveTab))
            {
                UpdateVisibility();
            }
        }

        private void UpdateVisibility()
        {
            switch (WagesMenu.ActiveTab)
            {
                case "WageRun":
                    CurrentView = WageRunVM;
                    break;
                case "Loans":
                    CurrentView = LoansVM;
                    break;
            }
        }
    }
}
