using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OCC.Shared.Models;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Infrastructure.Messages;
using OCC.WpfClient.Services.Interfaces;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace OCC.WpfClient.Features.SubContractorHub.ViewModels
{
    public partial class SnagListViewModel : ListViewModelBase<SnagJob>, IRecipient<CloseOverlayMessage>
    {
        private readonly ISnagService _snagService;
        private readonly IProjectService _projectService;
        private readonly ISubContractorService _subContractorService;
        private readonly IDialogService _dialogService;
        private readonly IProjectTaskService _taskService;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SnagListViewModel> _logger;

        public override string ReportTitle => "Snag List Report";
        public override List<ReportColumnDefinition> ReportColumns => new()
        {
            new() { Header = "Project", PropertyName = "ProjectName", Width = 2 },
            new() { Header = "Title", PropertyName = "Title", Width = 3 },
            new() { Header = "Priority", PropertyName = "Priority", Width = 1 },
            new() { Header = "Status", PropertyName = "Status", Width = 1.2 },
            new() { Header = "Due Date", PropertyName = "DueDate", Width = 1.2 }
        };

        [ObservableProperty] private ObservableCollection<Project> _projects = new();
        [ObservableProperty] private ObservableCollection<SubContractor> _subContractors = new();
        
        [ObservableProperty] private Project? _selectedFilterProject;
        [ObservableProperty] private SubContractor? _selectedFilterSubContractor;
        [ObservableProperty] private string _selectedStatus = "All Statuses";

        public ObservableCollection<string> Statuses { get; } = new()
        {
            "All Statuses", "Open", "InProgress", "Fixed", "Verified", "Closed"
        };

        [ObservableProperty] private bool _isStatusVisible = true;
        [ObservableProperty] private bool _isPartnerVisible = true;
        [ObservableProperty] private bool _isProjectVisible = true;
        [ObservableProperty] private bool _isDueDateVisible = true;

        public SnagListViewModel(
            ISnagService snagService,
            IProjectService projectService,
            ISubContractorService subContractorService,
            IDialogService dialogService,
            IProjectTaskService taskService,
            IServiceProvider serviceProvider,
            ILogger<SnagListViewModel> logger,
            IPdfService pdfService) : base(pdfService)
        {
            _snagService = snagService;
            _projectService = projectService;
            _subContractorService = subContractorService;
            _dialogService = dialogService;
            _taskService = taskService;
            _serviceProvider = serviceProvider;
            _logger = logger;
            Title = "Snag Management";
            
            _ = InitializeAsync();
            
            WeakReferenceMessenger.Default.Register(this);
        }

        public void Receive(CloseOverlayMessage message)
        {
            CloseOverlay();
            _ = LoadDataAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                IsBusy = true;
                var projectsTask = _projectService.GetProjectsAsync();
                var subContractorsTask = _subContractorService.GetSubContractorsAsync();
                
                await Task.WhenAll(projectsTask, subContractorsTask);
                
                Projects = new ObservableCollection<Project>(await projectsTask);
                SubContractors = new ObservableCollection<SubContractor>(await subContractorsTask);
                
                await LoadDataAsync();
            }
            finally
            {
                IsBusy = false;
            }
        }

        public override async Task LoadDataAsync()
        {
            try
            {
                IsBusy = true;
                IEnumerable<SnagJob> snags;

                if (SelectedFilterProject != null)
                    snags = await _snagService.GetProjectSnagJobsAsync(SelectedFilterProject.Id);
                else if (SelectedFilterSubContractor != null)
                    snags = await _snagService.GetSubContractorSnagJobsAsync(SelectedFilterSubContractor.Id);
                else
                    snags = await _snagService.GetSnagJobsAsync();

                var list = snags.ToList();
                
                // Apply Client-side status filter
                if (SelectedStatus != "All Statuses")
                {
                    list = list.Where(s => s.Status.ToString() == SelectedStatus).ToList();
                }

                Items = new ObservableCollection<SnagJob>(list);
                TotalCount = list.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading snag jobs");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void AddSnag()
        {
            var vm = _serviceProvider.GetRequiredService<SnagDetailViewModel>();
            OpenOverlay(vm);
        }

        [RelayCommand]
        private void EditSnag(SnagJob snag)
        {
            if (snag == null) return;
            var vm = _serviceProvider.GetRequiredService<SnagDetailViewModel>();
            vm.SetSnag(snag);
            OpenOverlay(vm);
        }

        partial void OnSelectedFilterProjectChanged(Project? value) => _ = LoadDataAsync();
        partial void OnSelectedFilterSubContractorChanged(SubContractor? value) => _ = LoadDataAsync();
        partial void OnSelectedStatusChanged(string value) => _ = LoadDataAsync();

        protected override void FilterItems()
        {
            // Basic search filter
            if (string.IsNullOrWhiteSpace(SearchQuery))
            {
                _ = LoadDataAsync();
                return;
            }

            var query = SearchQuery.ToLower();
            var filtered = Items.Where(s => 
                (s.Title?.ToLower().Contains(query) ?? false) ||
                (s.Description?.ToLower().Contains(query) ?? false) ||
                (s.SubContractor?.Name?.ToLower().Contains(query) ?? false) ||
                (s.Project?.Name?.ToLower().Contains(query) ?? false));

            Items = new ObservableCollection<SnagJob>(filtered);
        }
    }
}
