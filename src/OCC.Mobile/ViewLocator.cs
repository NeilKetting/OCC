using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using OCC.Mobile.ViewModels;

namespace OCC.Mobile
{
    public class ViewLocator : IDataTemplate
    {
        public Control? Build(object? data)
        {
            if (data is null)
                return null;

            var type = data.GetType();
            var name = type.FullName!.Replace("ViewModel", "View");
            var viewType = type.Assembly.GetType(name);

            if (viewType != null)
            {
                return (Control)Activator.CreateInstance(viewType)!;
            }

            return new TextBlock { Text = "Not Found: " + name };
        }

        public bool Match(object? data)
        {
            return data is ViewModelBase;
        }
    }
}
