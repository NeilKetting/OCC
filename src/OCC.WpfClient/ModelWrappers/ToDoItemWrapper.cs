using CommunityToolkit.Mvvm.ComponentModel;

namespace OCC.WpfClient.ModelWrappers
{
    public partial class ToDoItemWrapper : ObservableObject
    {
        [ObservableProperty] private bool _isChecked;
        [ObservableProperty] private string _content = string.Empty;

        public ToDoItemWrapper() { }

        public ToDoItemWrapper(string content, bool isChecked = false)
        {
            Content = content;
            IsChecked = isChecked;
        }
    }
}
