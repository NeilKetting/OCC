using CommunityToolkit.Mvvm.ComponentModel;
using OCC.Client.ViewModels.Core;
using OCC.Shared.Models;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System;
using System.Linq;
using OCC.Client.ModelWrappers;

namespace OCC.Client.Features.ProjectsHub.ViewModels
{
    public partial class ProjectCustomerReportViewModel : ViewModelBase
    {
        #region Observables - Project Details
        
        [ObservableProperty]
        private string _projectName = string.Empty;

        [ObservableProperty]
        private string _reportDate = DateTime.Today.ToString("yyyy/MM/dd");

        [ObservableProperty]
        private int _projectWeekNumber;

        [ObservableProperty]
        private string _projectStatus = "On track";

        [ObservableProperty]
        private string _customerName = string.Empty;

        [ObservableProperty]
        private string _storeName = string.Empty;

        [ObservableProperty]
        private string _contractorName = "Orange Circle Construction";

        [ObservableProperty]
        private string _projectManagerName = string.Empty;

        [ObservableProperty]
        private string _siteManagerName = "N/A";

        [ObservableProperty]
        private string _networkPMName = string.Empty;

        #endregion

        #region Observables - Program Of Works & Hours
        
        [ObservableProperty]
        private int _totalTasks;

        [ObservableProperty]
        private int _tasksInProgress;

        [ObservableProperty]
        private int _tasksCompleted;

        [ObservableProperty]
        private double _powPercentRequired;

        [ObservableProperty]
        private double _powPercentActual;

        [ObservableProperty]
        private int _delayDays;

        [ObservableProperty]
        private int _totalSafeWorkingHours;

        #endregion

        #region Observables - Status Summary & Dates

        [ObservableProperty]
        private string _statusSummary = string.Empty;

        [ObservableProperty]
        private DateTime? _siteEstablishmentPlanned;

        [ObservableProperty]
        private DateTime? _siteEstablishmentActual;

        [ObservableProperty]
        private DateTime? _practicalCompletionPlanned;

        [ObservableProperty]
        private DateTime? _practicalCompletionActual;

        #endregion

        #region Observables - Lists

        [ObservableProperty]
        private string _wasteDisposalType = "General waste";

        [ObservableProperty]
        private string _wasteGeneralQuantity = "0 TON";

        [ObservableProperty]
        private string _wasteRubbleQuantity = "0 m3";

        [ObservableProperty]
        private string _wasteScrapMetalsQuantity = "0 TON";

        [ObservableProperty]
        private string _wasteAsbestosQuantity = "0 TON";

        [ObservableProperty]
        private ObservableCollection<VendorReportItem> _vendorReports = new();

        [ObservableProperty]
        private ObservableCollection<VariationReportItem> _variationOrders = new();

        [ObservableProperty]
        private ObservableCollection<ProjectReportPhoto> _photos = new();

        #endregion

        public ProjectCustomerReportViewModel()
        {
            // Design time support / default initialization
        }

        public Task LoadReportDataAsync(Project project, ObservableCollection<ProjectTask> tasks, ObservableCollection<ProjectVariationOrderWrapper> variations)
        {
            if (project == null) return Task.CompletedTask;

            ProjectName = project.Name;
            StoreName = project.Name;
            
            // Try fetch customer. Usually Entity has Name, or fallback to the legacy string.
            CustomerName = project.CustomerEntity?.Name ?? project.Customer;
            if (string.IsNullOrEmpty(CustomerName)) CustomerName = "ENGEN"; // Fallback to ENGEN as per default layout

            ProjectManagerName = project.ProjectManager;
            if (project.SiteManager != null)
            {
                SiteManagerName = $"{project.SiteManager.FirstName} {project.SiteManager.LastName}".Trim();
            }

            // Calculate Tasks
            TotalTasks = tasks.Count;
            TasksCompleted = tasks.Count(t => t.IsComplete);
            TasksInProgress = TotalTasks - TasksCompleted; // Simplification, could be refined

            if (TotalTasks > 0)
            {
                PowPercentActual = Math.Round(((double)TasksCompleted / TotalTasks) * 100, 2);
            }

            // Calculate Dates (Using project dates vs actual if available)
            SiteEstablishmentPlanned = project.StartDate;
            PracticalCompletionPlanned = project.EndDate;

            // Load Variation Orders
            VariationOrders.Clear();
            int varCount = 1;
            foreach(var vo in variations)
            {
                VariationOrders.Add(new VariationReportItem 
                {
                    Number = varCount++,
                    Description = vo.Description,
                    Approved = string.IsNullOrEmpty(vo.ApprovedBy) ? "Pending" : "Approved",
                    Vendor = "OCC" // Placeholder logic
                });
            }

            // Populate dummy Vendor logic (usually this comes from a Vendor/Audit DB)
            VendorReports.Clear();
            VendorReports.Add(new VendorReportItem { Number = 1, VendorName = "OCC", Scope = "WW/SF", SafetyFileApproved = "Yes", ApprovalScore = 100, Audit1 = 98 });

            // Generate temporary dummy photos (In a real scenario, fetch this from IPhotoService or IProjectManager)
            Photos.Clear();

            return Task.CompletedTask;
        }
    }

    // --- Helper Models for DataGrid bindings --- //

    public class VendorReportItem
    {
        public int Number { get; set; }
        public string VendorName { get; set; } = string.Empty;
        public string Scope { get; set; } = string.Empty;
        public string SafetyFileApproved { get; set; } = string.Empty;
        public int? ApprovalScore { get; set; }
        public int? Audit1 { get; set; }
        public int? Audit2 { get; set; }
        public int? Audit3 { get; set; }
    }

    public class VariationReportItem
    {
        public int Number { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Approved { get; set; } = string.Empty;
        public string Vendor { get; set; } = string.Empty;
    }

    public class ProjectReportPhoto
    {
        public string PhotoUrl { get; set; } = string.Empty;
    }
}
