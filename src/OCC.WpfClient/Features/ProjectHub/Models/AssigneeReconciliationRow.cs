using CommunityToolkit.Mvvm.ComponentModel;
using OCC.Shared.Models;
using System;
using System.Collections.ObjectModel;

namespace OCC.WpfClient.Features.ProjectHub.Models
{
    public enum ReconciliationAction
    {
        MapToExisting,
        CreateNew,
        Skip
    }

    public partial class AssigneeReconciliationRow : ObservableObject
    {
        public string ImportedName { get; init; } = string.Empty;
        
        [ObservableProperty] private ReconciliationAction _action = ReconciliationAction.MapToExisting;
        [ObservableProperty] private AssigneeSelectionViewModel? _selectedMatch;
        
        [ObservableProperty] private string _email = string.Empty;
        [ObservableProperty] private string _phone = string.Empty;
        [ObservableProperty] private string _branch = string.Empty;
        [ObservableProperty] private string _address = string.Empty;
        [ObservableProperty] private string _specialties = string.Empty;

        public ObservableCollection<AssigneeSelectionViewModel> SuggestedMatches { get; } = new();
        public bool HasSuggestions => SuggestedMatches.Count > 0;

        public bool IsNew => Action == ReconciliationAction.CreateNew;
        public bool IsMapped => Action == ReconciliationAction.MapToExisting;
        public bool IsSkipped => Action == ReconciliationAction.Skip;

        public Action? OnActionUpdated { get; set; }

        partial void OnActionChanged(ReconciliationAction value)
        {
            OnPropertyChanged(nameof(IsNew));
            OnPropertyChanged(nameof(IsMapped));
            OnPropertyChanged(nameof(IsSkipped));
            OnActionUpdated?.Invoke();
        }
    }
}
