using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OCC.WpfClient.Infrastructure
{
    public partial class NavItem : ObservableObject
    {
        public string Label { get; }
        public string Route { get; }
        public string Category { get; }
        public string? IconColor { get; }
        public string? IconCode { get; }

        public ObservableCollection<NavItem> Children { get; } = new();
        public bool IsParent => Children.Any();

        [ObservableProperty]
        private bool _isActive;

        [ObservableProperty]
        private bool _isExpanded;

        [ObservableProperty]
        private int _unreadCount;

        public NavItem(string label, string route, string category, bool isActive = false, string? iconColor = null, string? iconCode = null)
        {
            Label = label;
            Route = route;
            Category = category;
            IsActive = isActive;
            IconColor = iconColor;
            IconCode = iconCode;
        }
    }
}
