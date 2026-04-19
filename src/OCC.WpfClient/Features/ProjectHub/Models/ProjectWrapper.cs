using OCC.Shared.Models;
using OCC.WpfClient.Infrastructure;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace OCC.WpfClient.Features.ProjectHub.Models
{
    public enum ProjectCreationMode
    {
        Quick,
        Comprehensive
    }

    public class ProjectWrapper : ViewModelBase
    {
        public Project Model { get; }

        public ProjectWrapper(Project model)
        {
            Model = model;
        }

        public Guid Id => Model.Id;

        public string Name
        {
            get => Model.Name;
            set { Model.Name = value; OnPropertyChanged(); }
        }

        public string ShortName
        {
            get => Model.ShortName;
            set { Model.ShortName = value; OnPropertyChanged(); }
        }

        public string Description
        {
            get => Model.Description;
            set { Model.Description = value; OnPropertyChanged(); }
        }

        public DateTime StartDate
        {
            get => Model.StartDate;
            set { Model.StartDate = value; OnPropertyChanged(); }
        }

        public DateTime EndDate
        {
            get => Model.EndDate;
            set { Model.EndDate = value; OnPropertyChanged(); }
        }

        public string Status
        {
            get => Model.Status;
            set { Model.Status = value; OnPropertyChanged(); }
        }

        public string Priority
        {
            get => Model.Priority;
            set { Model.Priority = value; OnPropertyChanged(); }
        }

        public string ProjectManager
        {
            get => Model.ProjectManager;
            set { Model.ProjectManager = value; OnPropertyChanged(); }
        }

        public string Location
        {
            get => Model.Location;
            set { Model.Location = value; OnPropertyChanged(); }
        }

        public Guid? SiteManagerId
        {
            get => Model.SiteManagerId;
            set { Model.SiteManagerId = value; OnPropertyChanged(); }
        }

        public Guid? CustomerId
        {
            get => Model.CustomerId;
            set { Model.CustomerId = value; OnPropertyChanged(); }
        }

        public string Customer
        {
            get => Model.Customer;
            set { Model.Customer = value; OnPropertyChanged(); }
        }

        // Location Info
        public string StreetLine1
        {
            get => Model.StreetLine1;
            set { Model.StreetLine1 = value; OnPropertyChanged(); }
        }

        public string StreetLine2
        {
            get => Model.StreetLine2 ?? string.Empty;
            set { Model.StreetLine2 = value; OnPropertyChanged(); }
        }

        public string City
        {
            get => Model.City;
            set { Model.City = value; OnPropertyChanged(); }
        }

        public string StateOrProvince
        {
            get => Model.StateOrProvince;
            set { Model.StateOrProvince = value; OnPropertyChanged(); }
        }

        public string PostalCode
        {
            get => Model.PostalCode;
            set { Model.PostalCode = value; OnPropertyChanged(); }
        }

        public string Country
        {
            get => Model.Country;
            set { Model.Country = value; OnPropertyChanged(); }
        }

        public double? Latitude
        {
            get => Model.Latitude;
            set { Model.Latitude = value; OnPropertyChanged(); }
        }

        public double? Longitude
        {
            get => Model.Longitude;
            set { Model.Longitude = value; OnPropertyChanged(); }
        }

        public bool HasValidationErrors { get; set; }
        public ObservableCollection<string> Errors { get; } = new();

        public void Validate(ProjectCreationMode mode)
        {
            Errors.Clear();
            if (string.IsNullOrWhiteSpace(Name)) Errors.Add("Project Name is required.");
            if (string.IsNullOrWhiteSpace(StreetLine1)) Errors.Add("Site Street Address is required.");
            if (CustomerId == null || CustomerId == Guid.Empty) Errors.Add("Customer selection is required.");
            //if (SiteManagerId == null || SiteManagerId == Guid.Empty) Errors.Add("Site Manager selection is required.");

            if (mode == ProjectCreationMode.Comprehensive)
            {
                if (string.IsNullOrWhiteSpace(ShortName)) Errors.Add("Project ID / Ref is required.");
                if (string.IsNullOrWhiteSpace(Status)) Errors.Add("Status is required.");
                if (string.IsNullOrWhiteSpace(Priority)) Errors.Add("Priority is required.");
                
                if (EndDate < StartDate) Errors.Add("End Date cannot be before Start Date.");
                
                // Detailed location for comprehensive
                if (string.IsNullOrWhiteSpace(City)) Errors.Add("City is required for full projects.");
                if (string.IsNullOrWhiteSpace(Country)) Errors.Add("Country is required for full projects.");
            }
            
            RefreshErrorState();
        }

        public void RefreshErrorState()
        {
            HasValidationErrors = Errors.Count > 0;
            OnPropertyChanged(nameof(HasValidationErrors));
            OnPropertyChanged(nameof(Errors));
        }
    }
}
