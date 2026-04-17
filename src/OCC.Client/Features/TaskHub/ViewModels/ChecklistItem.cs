using CommunityToolkit.Mvvm.ComponentModel;

namespace OCC.Client.Features.TaskHub.ViewModels
{
    public partial class ChecklistItem : ObservableObject
    {
        [ObservableProperty]
        private bool? _isChecked;

        [ObservableProperty]
        private string _content = string.Empty;

        public ChecklistItem(string content, bool? isChecked = false)
        {
            Content = content;
            IsChecked = isChecked;
        }
    }
}




