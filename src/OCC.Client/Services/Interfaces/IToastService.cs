using System;

namespace OCC.Client.Services.Interfaces
{
    public interface IToastService
    {
        void ShowSuccess(string title, string message);
        void ShowError(string title, string message);
        void ShowInfo(string title, string message);
        void ShowWarning(string title, string message);
    }
}
