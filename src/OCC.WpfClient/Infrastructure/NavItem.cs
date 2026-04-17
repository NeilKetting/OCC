using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OCC.WpfClient.Infrastructure
{
    public partial class NavItem : ObservableObject
    {
        public string Label { get; }
        public System.Windows.Media.Geometry? Icon { get; }
        public string Route { get; }
        public string Category { get; }

        public ObservableCollection<NavItem> Children { get; } = new();
        public bool IsParent => Children.Any();

        [ObservableProperty]
        private bool _isActive;

        [ObservableProperty]
        private bool _isExpanded;

        public NavItem(string label, string iconKey, string route, string category, bool isActive = false)
        {
            Label = label;
            Icon = System.Windows.Application.Current?.TryFindResource(iconKey) as System.Windows.Media.Geometry;
            Route = route;
            Category = category;
            IsActive = isActive;
        }
    }
}
