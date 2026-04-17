using CommunityToolkit.Mvvm.ComponentModel;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace OCC.Client.ModelWrappers
{
    /// <summary>
    /// A wrapper class for the Project model that provides validation and MVVM separation.
    /// </summary>
    public partial class ProjectWrapper : ObservableValidator
    {
        private readonly Project _model;

        public ProjectWrapper(Project model)
        {
            _model = model;
            Initialize();
        }

        public Project Model => _model;

        public Guid Id => _model.Id;

        [ObservableProperty]
        [Required(ErrorMessage = "Project name is required")]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        private DateTime _startDate = DateTime.Today;

        [ObservableProperty]
        private DateTime _endDate = DateTime.Today.AddMonths(1);

        [ObservableProperty]
        [Required(ErrorMessage = "Street address is required")]
        private string _streetLine1 = string.Empty;

        [ObservableProperty]
        private string? _streetLine2;

        [ObservableProperty]
        [Required(ErrorMessage = "City is required")]
        [MinLength(2, ErrorMessage = "City name is too short")]
        private string _city = string.Empty;

        [ObservableProperty]
        [Required(ErrorMessage = "State/Province is required")]
        [MinLength(2, ErrorMessage = "State/Province name is too short")]
        private string _stateOrProvince = string.Empty;

        [ObservableProperty]
        [Required(ErrorMessage = "Postal code is required")]
        private string _postalCode = string.Empty;

        [ObservableProperty]
        [Required(ErrorMessage = "Country is required")]
        [MinLength(2, ErrorMessage = "Country name is too short")]
        private string _country = "South Africa";

        [ObservableProperty]
        [Required(ErrorMessage = "GPS Latitude is required for geofencing. Please select an address from the search suggestions.")]
        [Range(-90.0, 90.0, ErrorMessage = "Invalid Latitude coordinate")]
        private double? _latitude;

        [ObservableProperty]
        [Required(ErrorMessage = "GPS Longitude is required for geofencing. Please select an address from the search suggestions.")]
        [Range(-180.0, 180.0, ErrorMessage = "Invalid Longitude coordinate")]
        private double? _longitude;

        [ObservableProperty]
        private string _status = "Planning";

        [ObservableProperty]
        private string _projectManager = string.Empty;

        [ObservableProperty]
        private string _location = string.Empty;

        [ObservableProperty]
        private Guid? _siteManagerId;

        [ObservableProperty]
        private string _customer = string.Empty;

        [ObservableProperty]
        private string _priority = "Medium";

        [ObservableProperty]
        private string _shortName = string.Empty;

        [ObservableProperty]
        private TimeSpan _workStartTime = new TimeSpan(8, 0, 0);

        [ObservableProperty]
        private TimeSpan _workEndTime = new TimeSpan(17, 0, 0);

        [ObservableProperty]
        private int _lunchDurationMinutes = 60;

        [ObservableProperty]
        private Guid? _customerId;

        public void Initialize()
        {
            Name = _model.Name;
            Description = _model.Description;
            StartDate = _model.StartDate == default ? DateTime.Today : _model.StartDate;
            EndDate = _model.EndDate == default ? DateTime.Today.AddMonths(1) : _model.EndDate;
            StreetLine1 = _model.StreetLine1;
            StreetLine2 = _model.StreetLine2;
            City = _model.City;
            StateOrProvince = _model.StateOrProvince;
            PostalCode = _model.PostalCode;
            Country = string.IsNullOrWhiteSpace(_model.Country) ? "South Africa" : _model.Country;
            Latitude = _model.Latitude;
            Longitude = _model.Longitude;
            Status = _model.Status;
            ProjectManager = _model.ProjectManager;
            Location = _model.Location;
            SiteManagerId = _model.SiteManagerId;
            Customer = _model.Customer;
            Priority = _model.Priority;
            ShortName = _model.ShortName;
            WorkStartTime = _model.WorkStartTime;
            WorkEndTime = _model.WorkEndTime;
            LunchDurationMinutes = _model.LunchDurationMinutes;
            CustomerId = _model.CustomerId;
        }

        public void CommitToModel()
        {
            _model.Name = Name;
            _model.Description = Description;
            _model.StartDate = StartDate;
            _model.EndDate = EndDate;
            _model.StreetLine1 = StreetLine1;
            _model.StreetLine2 = StreetLine2;
            _model.City = City;
            _model.StateOrProvince = StateOrProvince;
            _model.PostalCode = PostalCode;
            _model.Country = Country;
            _model.Latitude = Latitude;
            _model.Longitude = Longitude;
            _model.Status = Status;
            _model.ProjectManager = ProjectManager;
            _model.Location = Location;
            _model.SiteManagerId = SiteManagerId;
            _model.Customer = Customer;
            _model.Priority = Priority;
            _model.ShortName = ShortName;
            _model.WorkStartTime = WorkStartTime;
            _model.WorkEndTime = WorkEndTime;
            _model.LunchDurationMinutes = LunchDurationMinutes;
            _model.CustomerId = CustomerId;
        }

        public void Validate() => ValidateAllProperties();

        partial void OnNameChanged(string value) => ValidateProperty(value, nameof(Name));
        partial void OnStreetLine1Changed(string value) => ValidateProperty(value, nameof(StreetLine1));
        partial void OnCityChanged(string value) => ValidateProperty(value, nameof(City));
        partial void OnStateOrProvinceChanged(string value) => ValidateProperty(value, nameof(StateOrProvince));
        partial void OnPostalCodeChanged(string value) => ValidateProperty(value, nameof(PostalCode));
        partial void OnCountryChanged(string value) => ValidateProperty(value, nameof(Country));
        partial void OnLatitudeChanged(double? value) => ValidateProperty(value, nameof(Latitude));
        partial void OnLongitudeChanged(double? value) => ValidateProperty(value, nameof(Longitude));
    }
}
