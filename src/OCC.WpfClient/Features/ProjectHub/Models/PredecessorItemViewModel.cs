using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace OCC.WpfClient.Features.ProjectHub.Models
{
    public partial class PredecessorItemViewModel : ObservableObject
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayText))]
        private Guid _predecessorId;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayText))]
        private string _name = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayText))]
        private string _type = "FS";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayText))]
        private double _lagDays;

        public string DisplayText => $"{Name} ({Type}{(LagDays != 0 ? $" {(LagDays > 0 ? "+" : "")}{LagDays}d" : "")})";
    }
}
