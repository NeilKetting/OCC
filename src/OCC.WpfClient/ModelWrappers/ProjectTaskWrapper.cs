using CommunityToolkit.Mvvm.ComponentModel;
using OCC.Shared.Models;
using OCC.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.ComponentModel.DataAnnotations;

namespace OCC.WpfClient.ModelWrappers
{
    public partial class ProjectTaskWrapper : ObservableValidator
    {
        private ProjectTask _model;
        private bool _isUpdatingDuration;
        private bool _isInitializing;

        public ProjectTaskWrapper(ProjectTask model)
        {
            _model = model;
            Initialize();
        }

        public ProjectTask Model => _model;
        public Guid Id => _model.Id;
        public bool HasSubtasks => _model.Children != null && _model.Children.Any();
        public string DisplayId => $"T-{(_model.Id == Guid.Empty ? "NEW" : _model.Id.ToString().Substring(Math.Max(0, _model.Id.ToString().Length - 4)))}";
        public string AssigneeInitials => _model.AssigneeInitials;
        public void RefreshAssigneeInfo() => OnPropertyChanged(nameof(AssigneeInitials));

        [ObservableProperty]
        [Required(ErrorMessage = "Task name is required")]
        private string _name = string.Empty;

        public void Validate() => ValidateAllProperties();
        public new bool HasErrors => GetErrors().Any();

        partial void OnNameChanged(string value)
        {
            ValidateProperty(value, nameof(Name));
            _model.Name = value;
        }

        [ObservableProperty] private string _description = string.Empty;
        [ObservableProperty] private bool _isComplete;
        [ObservableProperty] private string _status = "Not Started";
        [ObservableProperty] private int _percentComplete;
        [ObservableProperty] private bool _isOnHold;
        [ObservableProperty] private string _priority = "Medium";
        
        [ObservableProperty]
        [CustomValidation(typeof(ProjectTaskWrapper), nameof(ValidateHoldReason))]
        private string _holdReason = string.Empty;
        [ObservableProperty] private DateTime? _startDate;
        [ObservableProperty] private DateTime? _finishDate;
        [ObservableProperty] private DateTime? _actualStartDate;
        [ObservableProperty] private DateTime? _actualCompleteDate;
        [ObservableProperty] private double? _plannedHours;
        [ObservableProperty] private double? _actualHours;
        [ObservableProperty] private string _plannedDurationText = string.Empty;
        [ObservableProperty] private string _actualDurationText = string.Empty;
        [ObservableProperty] private string _statusColor = "#CBD5E1";
        [ObservableProperty] private Guid? _ownerId;
        [ObservableProperty] private bool _isReminderSet;
        [ObservableProperty] private ReminderFrequency _frequency;
        [ObservableProperty] private DateTime? _nextReminderDate;
        [ObservableProperty] private string _assignedTo = string.Empty;
        [ObservableProperty] private Guid? _variationOrderId;

        private void Initialize()
        {
            _isInitializing = true;
            try
            {
                Name = _model.Name;
                Description = _model.Description;
                IsOnHold = _model.IsOnHold;
                Priority = _model.Priority;
                Status = _model.Status;
                PercentComplete = _model.PercentComplete;
                IsComplete = _model.IsComplete;
                HoldReason = _model.HoldReason;

                StartDate = _model.StartDate == DateTime.MinValue ? null : _model.StartDate;
                FinishDate = _model.FinishDate == DateTime.MinValue ? null : _model.FinishDate;
                ActualStartDate = _model.ActualStartDate;
                ActualCompleteDate = _model.ActualCompleteDate;
                
                PlannedHours = _model.PlannedDurationHours?.TotalHours;
                ActualHours = _model.ActualDuration?.TotalHours;
                
                if (!string.IsNullOrWhiteSpace(_model.Duration))
                {
                    PlannedDurationText = _model.Duration;
                }
                else if (string.IsNullOrEmpty(PlannedDurationText))
                {
                    UpdatePlannedDurationText();
                }
                if (string.IsNullOrEmpty(ActualDurationText)) UpdateActualDurationText();
                UpdateStatusColor();

                OwnerId = _model.OwnerId;
                IsReminderSet = _model.IsReminderSet;
                Frequency = _model.Frequency;
                NextReminderDate = _model.NextReminderDate;
                AssignedTo = _model.AssignedTo;
                VariationOrderId = _model.VariationOrderId;
            }
            finally
            {
                _isInitializing = false;
            }
        }

        public void CommitToModel()
        {
            _model.Name = Name;
            _model.Description = Description;
            _model.Status = Status;
            _model.PercentComplete = PercentComplete;
            _model.IsOnHold = IsOnHold;
            _model.HoldReason = HoldReason;
            _model.Priority = Priority;
            
            var safeMinDate = new DateTime(1753, 1, 1);
            _model.StartDate = StartDate ?? safeMinDate; 
            _model.FinishDate = FinishDate ?? safeMinDate;
            _model.ActualStartDate = ActualStartDate;
            _model.ActualCompleteDate = ActualCompleteDate;

            _model.PlannedDurationHours = PlannedHours.HasValue ? TimeSpan.FromHours(PlannedHours.Value) : null;
            _model.ActualDuration = ActualHours.HasValue ? TimeSpan.FromHours(ActualHours.Value) : null;
            _model.Duration = PlannedDurationText;

            _model.OwnerId = OwnerId;
            _model.IsReminderSet = IsReminderSet;
            _model.Frequency = Frequency;
            _model.NextReminderDate = NextReminderDate;
            _model.AssignedTo = AssignedTo;
            _model.VariationOrderId = VariationOrderId;
        }

        partial void OnDescriptionChanged(string value) => _model.Description = value;
        
        partial void OnIsCompleteChanged(bool value)
        {
            if (value)
            {
                if (ActualCompleteDate == null) ActualCompleteDate = DateTime.Now; 
                PercentComplete = 100;
                Status = "Completed"; 
            }
            else
            {
                if (PercentComplete == 100) PercentComplete = 50; 
                UpdateStatusFromPercent();
                ActualCompleteDate = null;
            }
            _model.ActualCompleteDate = ActualCompleteDate;
            _model.PercentComplete = PercentComplete;
            _model.Status = Status;
        }

        partial void OnStatusChanged(string value)
        {
            _model.Status = value;
            UpdateStatusColor();
            if (_isInitializing) return;

             switch(value)
            {
                case "Not Started":
                case "To Do":
                    PercentComplete = 0; 
                    break;
                case "Started": 
                    PercentComplete = 25; 
                    break;
                case "Halfway": 
                    PercentComplete = 50; 
                    break;
                case "Almost Done": 
                    PercentComplete = 75; 
                    break;
                case "Done": 
                case "Completed":
                    PercentComplete = 100; 
                    IsComplete = true; 
                    break;
            }
            if (value != "Done" && value != "Completed" && IsComplete) IsComplete = false;
        }

        partial void OnPercentCompleteChanged(int value)
        {
            _model.PercentComplete = value;
            if (value == 100 && !IsComplete) IsComplete = true;
            else if (value < 100 && IsComplete) IsComplete = false;
            else UpdateStatusFromPercent();
        }

        private void UpdateStatusFromPercent()
        {
            if (IsOnHold) return;
            if (PercentComplete >= 100) Status = "Completed";
            else if (PercentComplete >= 75) Status = "Almost Done";
            else if (PercentComplete >= 50) Status = "Halfway";
            else if (PercentComplete > 0) Status = "Started";
            else Status = "Not Started";
        }

        partial void OnIsOnHoldChanged(bool value)
        {
            _model.IsOnHold = value;
            if (value)
            {
                Status = "On Hold";
            }
            else if (Status == "On Hold")
            {
                UpdateStatusFromPercent();
            }
            UpdateStatusColor();
            ValidateProperty(HoldReason, nameof(HoldReason));
        }

        partial void OnHoldReasonChanged(string value)
        {
            _model.HoldReason = value;
            ValidateProperty(value, nameof(HoldReason));
        }

        public static ValidationResult? ValidateHoldReason(string? value, ValidationContext context)
        {
            var instance = (ProjectTaskWrapper)context.ObjectInstance;
            if (instance.IsOnHold && string.IsNullOrWhiteSpace(value))
            {
                return new ValidationResult("A reason is required when placing a task on hold.");
            }
            return ValidationResult.Success;
        }

        partial void OnPriorityChanged(string value) => _model.Priority = value;
        partial void OnOwnerIdChanged(Guid? value) => _model.OwnerId = value;
        partial void OnIsReminderSetChanged(bool value) => _model.IsReminderSet = value;
        partial void OnFrequencyChanged(ReminderFrequency value) => _model.Frequency = value;
        partial void OnNextReminderDateChanged(DateTime? value) => _model.NextReminderDate = value;

        partial void OnStartDateChanged(DateTime? value)
        {
            _model.StartDate = value ?? DateTime.MinValue;
            if (_isInitializing || _isUpdatingDuration) return;

            _isUpdatingDuration = true;
            try
            {
                if (value.HasValue)
                {
                    double durationDays = ParseDurationDays(PlannedDurationText);
                    if (durationDays > 0)
                    {
                        var newFinish = value.Value.Date.AddDays(durationDays > 1 ? durationDays - 1 : 0);
                        FinishDate = newFinish;
                        _model.FinishDate = newFinish;
                        PlannedHours = durationDays * 8.0;
                    }
                    else if (FinishDate.HasValue && FinishDate.Value >= value.Value)
                    {
                        var days = (FinishDate.Value.Date - value.Value.Date).TotalDays + 1;
                        PlannedDurationText = $"{days:0.#} {(days == 1 ? "day" : "days")}";
                        PlannedHours = days * 8.0;
                        _model.Duration = PlannedDurationText;
                    }
                }
            }
            finally
            {
                _isUpdatingDuration = false;
            }
        }

        partial void OnFinishDateChanged(DateTime? value)
        {
            _model.FinishDate = value ?? DateTime.MinValue;
            if (_isInitializing || _isUpdatingDuration) return;

            _isUpdatingDuration = true;
            try
            {
                if (StartDate.HasValue && value.HasValue && value.Value >= StartDate.Value)
                {
                    var days = (value.Value.Date - StartDate.Value.Date).TotalDays + 1;
                    PlannedDurationText = $"{days:0.#} {(days == 1 ? "day" : "days")}";
                    PlannedHours = days * 8.0;
                    _model.Duration = PlannedDurationText;
                }
            }
            finally
            {
                _isUpdatingDuration = false;
            }
        }

        partial void OnActualStartDateChanged(DateTime? value) { _model.ActualStartDate = value; UpdateActualDurationText(); }
        partial void OnActualCompleteDateChanged(DateTime? value) { _model.ActualCompleteDate = value; UpdateActualDurationText(); }

        partial void OnPlannedHoursChanged(double? value)
        {
            if (_isUpdatingDuration) return;
            if (value.HasValue)
            {
                _isUpdatingDuration = true;
                double days = value.Value / 8.0;
                PlannedDurationText = $"{days:0.#} {(days == 1 ? "day" : "days")}";
                _isUpdatingDuration = false;
            }
        }

        partial void OnPlannedDurationTextChanged(string value) { if (_isUpdatingDuration) return; FormatDuration(value, isPlanned: true); }
        partial void OnActualDurationTextChanged(string value) { if (_isUpdatingDuration) return; FormatDuration(value, isPlanned: false); }

        private void UpdateStatusColor() => StatusColor = _model.StatusColor;
        private double CalculatePlannedHours(DateTime start, DateTime end) => Math.Round(((end.Date - start.Date).TotalDays + 1) * 8, 1);

        private double ParseDurationDays(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Equals("None", StringComparison.OrdinalIgnoreCase)) return 0;
            var numericPart = new string(text.Where(c => char.IsDigit(c) || c == '.').ToArray());
            if (!double.TryParse(numericPart, NumberStyles.Any, CultureInfo.InvariantCulture, out double val) || val <= 0)
            {
                return 0;
            }

            var lower = text.ToLowerInvariant();
            if (lower.Contains("hr") || lower.Contains("hour") || lower.EndsWith("h"))
            {
                return Math.Round(val / 8.0, 2); // 8 hours per workday
            }
            if (lower.Contains("wk") || lower.Contains("week") || lower.EndsWith("w"))
            {
                return val * 5.0; // 5 workdays per week
            }

            return val; // Days default
        }

        private string FormatDurationText(double days, string rawInput)
        {
            var lower = (rawInput ?? string.Empty).ToLowerInvariant();
            if (lower.Contains("hr") || lower.Contains("hour") || lower.EndsWith("h"))
            {
                double hours = Math.Round(days * 8.0, 1);
                return $"{hours:0.#} {(hours == 1 ? "hour" : "hours")}";
            }
            if (lower.Contains("wk") || lower.Contains("week") || lower.EndsWith("w"))
            {
                double weeks = Math.Round(days / 5.0, 1);
                return $"{weeks:0.#} {(weeks == 1 ? "week" : "weeks")}";
            }
            return $"{days:0.#} {(days == 1 ? "day" : "days")}";
        }

        private void UpdatePlannedDurationText()
        {
            _isUpdatingDuration = true;
            if (StartDate.HasValue && FinishDate.HasValue)
            {
                var days = (FinishDate.Value.Date - StartDate.Value.Date).TotalDays + 1;
                PlannedDurationText = FormatDurationText(days, PlannedDurationText);
                _model.Duration = PlannedDurationText;
            }
            else PlannedDurationText = "None";
            _isUpdatingDuration = false;
        }

        private void UpdateActualDurationText()
        {
            _isUpdatingDuration = true;
            if (ActualStartDate.HasValue && ActualCompleteDate.HasValue)
            {
                var days = (ActualCompleteDate.Value.Date - ActualStartDate.Value.Date).TotalDays + 1;
                ActualDurationText = FormatDurationText(days, ActualDurationText);
            }
            else ActualDurationText = "None";
            _isUpdatingDuration = false;
        }

        private void FormatDuration(string value, bool isPlanned)
        {
            if (_isInitializing) return;
            if (string.IsNullOrWhiteSpace(value) || value == "None") return;
            double days = ParseDurationDays(value);
            if (days > 0)
            {
                _isUpdatingDuration = true;
                try
                {
                    var formatted = FormatDurationText(days, value);
                    if (isPlanned)
                    {
                        PlannedDurationText = formatted;
                        PlannedHours = days * 8.0;
                        _model.Duration = formatted;
                        if (StartDate.HasValue)
                        {
                            var newFinish = StartDate.Value.Date.AddDays(days > 1 ? days - 1 : 0);
                            FinishDate = newFinish;
                            _model.FinishDate = newFinish;
                        }
                    }
                    else
                    {
                        ActualDurationText = formatted;
                        ActualHours = days * 8.0;
                    }
                }
                finally
                {
                    _isUpdatingDuration = false;
                }
            }
        }
    }
}
