namespace OCC.Mobile.Services
{
    using System.Threading.Tasks;

    public interface IUpdateService
    {
        string CurrentVersion { get; }
        Task<UpdateCheckResult> CheckForUpdatesAsync();
        Task<string> DownloadUpdateAsync(UpdateCheckResult update, Action<double> progress);
    }

    public class UpdateCheckResult
    {
        public bool IsUpdateAvailable { get; set; }
        public string LatestVersion { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public string ReleaseNotes { get; set; } = string.Empty;
    }
}
