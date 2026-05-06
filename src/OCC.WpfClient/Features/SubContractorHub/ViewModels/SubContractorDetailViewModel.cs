using System;
using System.Collections.Generic;
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

namespace OCC.WpfClient.Features.SubContractorHub.ViewModels
{
    public partial class SubContractorDetailViewModel : DetailViewModelBase
    {
        private readonly SubContractorListViewModel _parent;
        private readonly ISubContractorService _subContractorService;
        private readonly IUserService _userService;
        private readonly SubContractor _model;

        [ObservableProperty] private string _name;
        [ObservableProperty] private string _email;
        [ObservableProperty] private string _phone;
        [ObservableProperty] private string _address;
        [ObservableProperty] private string _branch;
        [ObservableProperty] private string _colorTheme;
        
        [ObservableProperty] private bool _hasPortalAccess;
        [ObservableProperty] private string _portalPassword = string.Empty;
        [ObservableProperty] private string _linkedUserName = "No Portal Account Linked";
        [ObservableProperty] private bool _isPasswordVisible;
        
        public ObservableCollection<SpecialtyOption> SpecialtyOptions { get; } = new();

        public bool IsNew => _model.Id == Guid.Empty;

        public SubContractorDetailViewModel(
            SubContractorListViewModel parent,
            SubContractor model,
            ISubContractorService subContractorService,
            IUserService userService,
            IDialogService dialogService,
            ILogger logger,
            IPdfService pdfService) : base(dialogService, logger, pdfService)
        {
            _parent = parent;
            _model = model;
            _subContractorService = subContractorService;
            _userService = userService;

            Title = IsNew ? "New Sub-Contractor" : $"Edit {model.Name}";
            
            _name = model.Name;
            _email = model.Email ?? string.Empty;
            _phone = model.Phone ?? string.Empty;
            _address = model.Address ?? string.Empty;
            _branch = model.Branch;
            _colorTheme = model.ColorTheme ?? string.Empty;

            _hasPortalAccess = model.PortalUserId != null;
            if (_hasPortalAccess)
            {
                _ = LoadLinkedUserAsync();
            }

            if (IsNew)
            {
                // Generate color dynamically after initialization
                _ = GenerateUniqueColorAsync();
            }

            InitializeSpecialties(model.Specialties);
        }

        private async Task LoadLinkedUserAsync()
        {
            if (_model.PortalUserId.HasValue)
            {
                var user = await _userService.GetUserAsync(_model.PortalUserId.Value);
                if (user != null)
                {
                    LinkedUserName = user.Email;
                }
            }
        }

        private async Task GenerateUniqueColorAsync()
        {
            try
            {
                var existing = await _subContractorService.GetSubContractorSummariesAsync();
                var usedColors = existing.Select(e => e.ColorTheme?.ToUpperInvariant()).Where(c => !string.IsNullOrEmpty(c)).ToHashSet();

                var random = new Random();
                string generated;
                do
                {
                    generated = $"#{random.Next(0x1000000):X6}";
                } while (usedColors.Contains(generated));

                ColorTheme = generated;
            }
            catch
            {
                // Fallback if network fails during creation setup
                ColorTheme = $"#{new Random().Next(0x1000000):X6}";
            }
        }

        private void InitializeSpecialties(string? specialties)
        {
            var selectedList = (specialties ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            var allSpecialties = new[]
            {
                "General Contracting", "Demolition", "Excavation & Earth Moving", "Foundations & Concrete",
                "Masonry", "Structural Steel", "Carpentry (Rough)", "Carpentry (Finish)", "Roofing",
                "Siding & Exterior Trim", "Insulation", "Drywall & Plaster", "Painting & Wallcovering",
                "Flooring", "Cabinetry & Woodwork", "Countertops", "Windows & Doors", "Electrical",
                "Plumbing", "HVAC", "Fire Protection", "Low Voltage", "Landscaping", "Paving & Asphalt",
                "Fencing", "Cleaning"
            };

            foreach (var s in allSpecialties)
            {
                SpecialtyOptions.Add(new SpecialtyOption
                {
                    Name = s,
                    IsChecked = selectedList.Contains(s)
                });
            }
        }

        protected override async Task ExecuteSaveAsync()
        {
            _model.Name = Name;
            _model.Email = Email;
            _model.Phone = Phone;
            _model.Address = Address;
            _model.Branch = Branch;
            _model.ColorTheme = ColorTheme;
            
            var selectedSpecialties = SpecialtyOptions
                .Where(x => x.IsChecked)
                .Select(x => x.Name);
            
            _model.Specialties = string.Join(", ", selectedSpecialties);

            // Portal Access Logic
            if (HasPortalAccess && _model.PortalUserId == null)
            {
                // Try to find user by email first
                var users = await _userService.GetUsersAsync();
                var existingUser = users.FirstOrDefault(u => u.Email.Equals(Email, System.StringComparison.OrdinalIgnoreCase));
                
                if (existingUser != null)
                {
                    _model.PortalUserId = existingUser.Id;
                }
                else if (!string.IsNullOrWhiteSpace(PortalPassword))
                {
                    // Create new user
                    var newUser = new User
                    {
                        Email = Email,
                        Password = PortalPassword,
                        FirstName = Name.Split(' ')[0],
                        LastName = Name.Contains(" ") ? Name.Substring(Name.IndexOf(" ") + 1) : "Contractor",
                        UserRole = UserRole.ExternalContractor,
                        IsApproved = true
                    };
                    
                    var success = await _userService.CreateUserAsync(newUser);
                    if (success)
                    {
                        var allUsers = await _userService.GetUsersAsync();
                        var created = allUsers.FirstOrDefault(u => u.Email.Equals(Email, System.StringComparison.OrdinalIgnoreCase));
                        if (created != null) _model.PortalUserId = created.Id;
                    }
                }
            }
            else if (!HasPortalAccess)
            {
                _model.PortalUserId = null;
            }

            if (IsNew)
            {
                await _subContractorService.CreateSubContractorAsync(_model);
            }
            else
            {
                var success = await _subContractorService.UpdateSubContractorAsync(_model);
                if (!success)
                {
                    throw new System.Exception("Failed to update sub-contractor. Please check your connection.");
                }
            }
        }

        protected override async Task<bool> ExecuteForceSaveAsync()
        {
            try
            {
                var latest = await _subContractorService.GetSubContractorAsync(_model.Id);
                if (latest != null)
                {
                    _model.RowVersion = latest.RowVersion;
                    var success = await _subContractorService.UpdateSubContractorAsync(_model);
                    return success;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during force save");
                return false;
            }
        }

        protected override async Task<bool> ValidateAsync()
        {
            ValidationErrors.Clear();
            if (string.IsNullOrWhiteSpace(Name))
            {
                ValidationErrors.Add("Company name is required.");
                HasErrors = true;
                await PulseValidationAsync();
                return false;
            }
            HasErrors = false;
            return true;
        }

        protected override void OnSaveSuccess()
        {
            NotifySuccess("Success", $"Sub-contractor '{Name}' saved successfully.");
            _parent.LoadDataAsync().ConfigureAwait(false);
            _parent.CloseDetailView();
        }

        protected override async Task ExecuteReloadAsync()
        {
            var latest = await _subContractorService.GetSubContractorAsync(_model.Id);
            if (latest != null)
            {
                _model.Name = latest.Name;
                _model.Email = latest.Email;
                _model.Phone = latest.Phone;
                _model.Address = latest.Address;
                _model.Specialties = latest.Specialties;
                _model.RowVersion = latest.RowVersion;
                _model.ColorTheme = latest.ColorTheme;

                Name = _model.Name;
                Email = _model.Email ?? string.Empty;
                Phone = _model.Phone ?? string.Empty;
                Address = _model.Address ?? string.Empty;
                Branch = _model.Branch;
                ColorTheme = _model.ColorTheme ?? string.Empty;

                SpecialtyOptions.Clear();
                InitializeSpecialties(_model.Specialties);
                
                Title = $"Edit {Name} (Reloaded)";
            }
        }

        protected override void OnCancel()
        {
            _parent.CloseDetailView();
        }

        protected override string GetReportTitle() => $"Sub-Contractor Profile: {Name}";
        protected override object GetReportItem() => new
        {
            Name,
            Email,
            Phone,
            Address,
            Branch,
            Specialties = string.Join(", ", SpecialtyOptions.Where(x => x.IsChecked).Select(x => x.Name))
        };
    }

    public partial class SpecialtyOption : ObservableObject
    {
        [ObservableProperty] private string _name = string.Empty;
        [ObservableProperty] private bool _isChecked;
    }
}
