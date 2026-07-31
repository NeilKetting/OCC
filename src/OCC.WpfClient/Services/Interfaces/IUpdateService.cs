using Velopack;

namespace OCC.WpfClient.Services.Interfaces
{
    public interface IUpdateService
    {
        string CurrentVersion { get; }
        Task<UpdateInfo?> CheckForUpdatesAsync();
        Task DownloadUpdatesAsync(UpdateInfo newVersion, Action<int> progress);
        void ApplyUpdatesAndRestart(UpdateInfo newVersion);

        /// <summary>Natively purges the local Velopack package cache and temporary directories.</summary>
        void PurgeUpdateCache();
    }
}
