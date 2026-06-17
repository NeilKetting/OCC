using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Mobile.ViewModels;
using OCC.Mobile.Services;
using OCC.Shared.Models;
using Avalonia;
using Avalonia.Media;

namespace OCC.Mobile.Features.Dashboard
{
    public class ProjectCardViewModel : ObservableObject
    {
        public Project Project { get; }

        public Guid Id => Project.Id;
        public string Name => Project.Name;
        public string Location => Project.Location;

        public int TasksDone => Project.CompletedTaskCount;
        public int TasksTotal => Project.TotalTaskCount;
        public double Progress => Project.Progress;
        
        public int ProgressPct => Project.TotalTaskCount > 0 
            ? (int)Math.Round((double)Project.CompletedTaskCount / Project.TotalTaskCount * 100) 
            : 0;

        public string TaskProgressString => Project.TaskProgressString;

        public int DueToday => Project.Tasks.Count(t => !t.IsComplete && t.FinishDate.Date == DateTime.Today);
        public int Overdue => Project.Tasks.Count(t => t.IsOverdue);
        public int TeamCount => Project.TeamMembers.Count;
        public string StartDate => Project.StartDate.ToString("dd MMM yyyy");
        public string EndDate => Project.EndDate != default ? Project.EndDate.ToString("dd MMM yyyy") : "—";

        public string StatusDisplay
        {
            get
            {
                if (Project.Status == "Completed" || Math.Round(Progress) >= 100) return "Completed";
                if (Project.Status == "OnHold" || Project.Status == "On Hold") return "On Hold";
                if (Project.Status == "Not Started" || Math.Round(Progress) == 0) return "Not Started";
                
                // Overdue tasks check
                if (Overdue > 5 || (Overdue > 0 && Progress < 20)) return "At Risk";
                
                return "On Track";
            }
        }

        public string AccentFrom { get; }
        public string AccentTo { get; }

        public IBrush CardHeaderBackground
        {
            get
            {
                var from = Color.Parse(AccentFrom);
                var to = Color.Parse(AccentTo);
                var fromTint = Color.FromArgb(0x22, from.R, from.G, from.B);
                var toTint = Color.FromArgb(0x18, to.R, to.G, to.B);
                return new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                    GradientStops = new GradientStops
                    {
                        new GradientStop(fromTint, 0),
                        new GradientStop(toTint, 1)
                    }
                };
            }
        }

        public IBrush AccentBrush => new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops = new GradientStops
            {
                new GradientStop(Color.Parse(AccentFrom), 0),
                new GradientStop(Color.Parse(AccentTo), 1)
            }
        };

        public double SweepAngle => 240.0 * (ProgressPct / 100.0);

        // Status colors
        public string StatusColor => StatusDisplay switch
        {
            "On Track" => "#34D399",   // Emerald-400
            "At Risk" => "#F87171",    // Red-400
            "Completed" => "#22D3EE",  // Cyan-400
            "On Hold" => "#FBBF24",    // Amber-400
            "Not Started" => "#94A3B8", // Slate-400
            _ => "#94A3B8"
        };

        public string StatusBg => StatusDisplay switch
        {
            "On Track" => "#1A34D399",
            "At Risk" => "#1AF87171",
            "Completed" => "#1A22D3EE",
            "On Hold" => "#1AFBBF24",
            "Not Started" => "#1A94A3B8",
            _ => "#1A94A3B8"
        };

        public string StatusBorder => StatusDisplay switch
        {
            "On Track" => "#3334D399",
            "At Risk" => "#33F87171",
            "Completed" => "#3322D3EE",
            "On Hold" => "#33FBBF24",
            "Not Started" => "#3394A3B8",
            _ => "#3394A3B8"
        };

        public ProjectCardViewModel(Project project, int index)
        {
            Project = project;

            // Accent gradient colors
            var colors = new[]
            {
                new { From = "#6366F1", To = "#22D3EE" }, // Indigo to Cyan
                new { From = "#10B981", To = "#22D3EE" }, // Emerald to Cyan
                new { From = "#6366F1", To = "#8B5CF6" }, // Indigo to Violet
            };
            var sel = colors[index % colors.Length];
            AccentFrom = sel.From;
            AccentTo = sel.To;
        }
    }

    public partial class ActiveProjectsViewModel : ViewModelBase
    {
        private readonly INavigationService _navigationService;
        private readonly IProjectService _projectService;
        private readonly ISignalRService _signalRService;
        private readonly System.Threading.SemaphoreSlim _loadSemaphore = new(1, 1);

        private readonly List<ProjectCardViewModel> _allProjects = new();

        [ObservableProperty]
        private ObservableCollection<ProjectCardViewModel> _activeProjects = new();

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private string _selectedFilter = "All";

        // Summary Statistics
        [ObservableProperty]
        private int _totalProjectsCount;

        [ObservableProperty]
        private string _totalProgressString = "0/0";

        [ObservableProperty]
        private int _totalOverdueCount;

        [ObservableProperty]
        private int _totalAtRiskCount;

        [ObservableProperty]
        private int _overallTasksCount;

        public List<string> Filters { get; } = new() { "All", "On Track", "At Risk", "On Hold", "Not Started", "Completed" };

        public ActiveProjectsViewModel(INavigationService navigationService, IProjectService projectService, ISignalRService signalRService)
        {
            _navigationService = navigationService;
            _projectService = projectService;
            _signalRService = signalRService;
            
            _signalRService.EntityUpdated += OnEntityUpdated;
            
            Title = "Active Projects";
            LoadData().FireAndForget();
        }

        private void OnEntityUpdated(string entityType, string action, Guid id)
        {
            if (entityType == "Project" || entityType == "ProjectTask")
            {
                LoadData().FireAndForget();
            }
        }

        public override void Dispose()
        {
            _signalRService.EntityUpdated -= OnEntityUpdated;
            base.Dispose();
        }

        partial void OnSearchTextChanged(string value) => FilterProjects();
        partial void OnSelectedFilterChanged(string value) => FilterProjects();

        [RelayCommand]
        private void SetFilter(string filter)
        {
            SelectedFilter = filter;
        }

        private void FilterProjects()
        {
            ActiveProjects.Clear();
            var filtered = _allProjects.Where(p =>
            {
                bool matchesSearch = string.IsNullOrWhiteSpace(SearchText) ||
                                     p.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                                     p.Location.Contains(SearchText, StringComparison.OrdinalIgnoreCase);

                bool matchesFilter = SelectedFilter == "All" || 
                                     p.StatusDisplay.Equals(SelectedFilter, StringComparison.OrdinalIgnoreCase);

                return matchesSearch && matchesFilter;
            }).ToList();

            foreach (var card in filtered)
            {
                ActiveProjects.Add(card);
            }

            // Update stats
            TotalProjectsCount = filtered.Count;
            TotalOverdueCount = filtered.Sum(p => p.Overdue);
            TotalAtRiskCount = filtered.Count(p => p.StatusDisplay == "At Risk");
            
            var done = filtered.Sum(p => p.TasksDone);
            var total = filtered.Sum(p => p.TasksTotal);
            TotalProgressString = $"{done}/{total}";
            OverallTasksCount = total;
        }

        public async Task LoadData()
        {
            if (!await _loadSemaphore.WaitAsync(0)) return;
            try
            {
                var projects = await _projectService.GetProjectsAsync(assignedToMe: true);
                var projectList = projects.GroupBy(p => p.Id).Select(g => g.First()).ToList(); 
                
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => 
                {
                    _allProjects.Clear();
                    for (int i = 0; i < projectList.Count; i++)
                    {
                        _allProjects.Add(new ProjectCardViewModel(projectList[i], i));
                    }
                    FilterProjects();
                });
            }
            finally
            {
                _loadSemaphore.Release();
            }
        }

        [RelayCommand]
        private void NavigateToProjectTasks(ProjectCardViewModel card)
        {
            if (card != null)
            {
                _navigationService.NavigateTo<Features.Tasks.RedesignTasksViewModel>(vm => vm.ProjectId = card.Id);
            }
        }

        [RelayCommand]
        private void NavigateToDueToday(ProjectCardViewModel card)
        {
            if (card != null)
            {
                _navigationService.NavigateTo<Features.Tasks.RedesignTasksViewModel>(vm => 
                {
                    vm.ProjectId = card.Id;
                    vm.ShowDueTodayOnly = true;
                });
            }
        }

        [RelayCommand]
        private void NavigateToOverdue(ProjectCardViewModel card)
        {
            if (card != null)
            {
                _navigationService.NavigateTo<Features.Tasks.RedesignTasksViewModel>(vm => 
                {
                    vm.ProjectId = card.Id;
                    vm.ShowOverdueOnly = true;
                });
            }
        }

        [RelayCommand]
        private void NavigateToInventory(ProjectCardViewModel card)
        {
            if (card != null)
            {
                _navigationService.NavigateTo<InventoryViewModel>(vm => 
                {
                    vm.ProjectId = card.Id;
                    vm.LoadDataCommand.Execute(null);
                });
            }
        }

        [RelayCommand]
        private void NavigateToReceiveCrew(ProjectCardViewModel card)
        {
            if (card != null)
            {
                _navigationService.NavigateTo<ReceiveCrewViewModel>(vm => 
                {
                    vm.TargetProjectId = card.Id;
                    vm.LoadDataCommand.Execute(null);
                });
            }
        }

        [RelayCommand]
        private void NavigateToProjectHseq(ProjectCardViewModel card)
        {
            if (card != null)
            {
                _navigationService.NavigateTo<HSEQ.HseqListViewModel>(vm => vm.ProjectId = card.Id);
            }
        }
    }
}
