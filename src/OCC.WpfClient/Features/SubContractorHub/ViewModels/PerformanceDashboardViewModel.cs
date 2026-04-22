using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OCC.Shared.Models;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Services.Interfaces;

namespace OCC.WpfClient.Features.SubContractorHub.ViewModels
{
    public partial class PerformanceDashboardViewModel : ViewModelBase
    {
        private readonly ISubContractorService _subContractorService;
        private readonly ISnagService _snagService;
        private readonly ILogger<PerformanceDashboardViewModel> _logger;

        [ObservableProperty] private decimal _averageRating;
        [ObservableProperty] private decimal _averageOnTimeRate;
        [ObservableProperty] private int _totalActiveSnags;
        [ObservableProperty] private int _totalPartners;

        [ObservableProperty] private int _diamondCount;
        [ObservableProperty] private int _goldCount;
        [ObservableProperty] private int _silverCount;
        [ObservableProperty] private int _bronzeCount;

        [ObservableProperty] private ObservableCollection<SubContractorPerformanceDto> _topPerformers = new();
        [ObservableProperty] private ObservableCollection<SubContractorPerformanceDto> _lowPerformers = new();
        [ObservableProperty] private ObservableCollection<SubContractor> _allSubContractors = new();

        public PerformanceDashboardViewModel(
            ISubContractorService subContractorService,
            ISnagService snagService,
            ILogger<PerformanceDashboardViewModel> logger)
        {
            _subContractorService = subContractorService;
            _snagService = snagService;
            _logger = logger;
            Title = "Partner Performance Hub";

            _ = LoadDataAsync();
        }

        public async Task LoadDataAsync()
        {
            try
            {
                IsBusy = true;
                UpdateStatus("Analyzing partner performance...");

                var contractors = await _subContractorService.GetSubContractorsAsync();
                var contractorsList = contractors.ToList();
                AllSubContractors = new ObservableCollection<SubContractor>(contractorsList);

                if (contractorsList.Any())
                {
                    AverageRating = contractorsList.Average(c => c.Rating);
                    AverageOnTimeRate = contractorsList.Average(c => c.OnTimeRate);
                    TotalPartners = contractorsList.Count;

                    DiamondCount = contractorsList.Count(c => c.PerformanceTier == "Diamond");
                    GoldCount = contractorsList.Count(c => c.PerformanceTier == "Gold");
                    SilverCount = contractorsList.Count(c => c.PerformanceTier == "Silver");
                    BronzeCount = contractorsList.Count(c => c.PerformanceTier == "Bronze");
                }

                var snags = await _snagService.GetSnagJobsAsync();
                TotalActiveSnags = snags.Count(s => s.Status != SnagStatus.Closed && s.Status != SnagStatus.Verified);

                // Map to DTOs for UI display
                var performanceList = contractorsList.Select(c => new SubContractorPerformanceDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    Rating = c.Rating,
                    PerformanceTier = c.PerformanceTier,
                    OnTimeRate = c.OnTimeRate,
                    TotalTasks = c.CompletedTasksCount,
                    TotalSnags = c.TotalSnagsCount,
                    ColorTheme = c.ColorTheme ?? "#AccentBlue",
                    Specialties = c.Specialties
                }).ToList();

                TopPerformers = new ObservableCollection<SubContractorPerformanceDto>(
                    performanceList.OrderByDescending(p => p.Rating).Take(5));

                LowPerformers = new ObservableCollection<SubContractorPerformanceDto>(
                    performanceList.OrderBy(p => p.Rating).Take(5));

                UpdateStatus("Ready");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading performance dashboard");
                UpdateStatus("Error loading data");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void ViewDetails(SubContractorPerformanceDto? partner)
        {
            // TODO: Navigate to partner profile or open overlay
        }
    }

    public class SubContractorPerformanceDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Rating { get; set; }
        public string PerformanceTier { get; set; } = string.Empty;
        public decimal OnTimeRate { get; set; }
        public int TotalTasks { get; set; }
        public int TotalSnags { get; set; }
        public string ColorTheme { get; set; } = string.Empty;
        public string? Specialties { get; set; }
    }
}
