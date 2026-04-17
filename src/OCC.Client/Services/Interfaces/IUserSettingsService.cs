using System.Collections.Generic;

namespace OCC.Client.Services.Interfaces
{
    public interface IUserSettingsService
    {
        T GetSetting<T>(string key, T defaultValue);
        void SaveSetting<T>(string key, T value);
    }
}
