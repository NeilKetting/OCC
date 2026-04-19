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
        
        public ObservableCollection<AssigneeSelectionViewModel> SuggestedMatches { get; } = new();

        public bool IsNew => Action == ReconciliationAction.CreateNew;
        public bool IsMapped => Action == ReconciliationAction.MapToExisting;
        public bool IsSkipped => Action == ReconciliationAction.Skip;

        partial void OnActionChanged(ReconciliationAction value)
        {
            OnPropertyChanged(nameof(IsNew));
            OnPropertyChanged(nameof(IsMapped));
            OnPropertyChanged(nameof(IsSkipped));
        }
    }
}
