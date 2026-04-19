using CommunityToolkit.Mvvm.ComponentModel;
using OCC.Shared.Models;
using System;

namespace OCC.WpfClient.Features.ProjectHub.Models
{
    public partial class AssigneeSelectionViewModel : ObservableObject
    {
        [ObservableProperty] private bool _isSelected;
        
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Role { get; init; } = string.Empty;
        public AssigneeType Type { get; init; }
        public string Branch { get; init; } = string.Empty;
        
        public string DisplayName => $"{Name} ({Role})";
    }
}
