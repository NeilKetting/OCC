using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace OCC.Client.Models
{
    public partial class ToastMessage : ObservableObject
    {
        public Guid Id { get; } = Guid.NewGuid();
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public ToastType Type { get; set; } = ToastType.Info;
        public DateTime CreatedAt { get; } = DateTime.Now;

        [ObservableProperty]
        private double _opacity = 1.0;

        public bool IsInfo => Type == ToastType.Info;
        public bool IsSuccess => Type == ToastType.Success;
        public bool IsWarning => Type == ToastType.Warning;
        public bool IsError => Type == ToastType.Error;
    }

    public enum ToastType
    {
        Info,
        Success,
        Warning,
        Error
    }
}
