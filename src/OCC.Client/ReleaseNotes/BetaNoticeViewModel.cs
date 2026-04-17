using CommunityToolkit.Mvvm.Input;
using System;
using System.IO;

namespace OCC.Client.ReleaseNotes
{
    public partial class BetaNoticeViewModel : ViewModels.Core.ViewModelBase
    {
        public string VersionText { get; }
        private readonly string _rawVersion;

        public event Action? Accepted;
        public event Action? OpenReleaseNotesRequested;

        [RelayCommand]
        private void OpenReleaseNotes()
        {
            OpenReleaseNotesRequested?.Invoke();
        }

        public BetaNoticeViewModel(string version)
        {
            _rawVersion = version;
            VersionText = $"Version: {version} (BETA)";
        }
        
        // Parameterless for Design Time
        public BetaNoticeViewModel() : this("1.0.0-DEV") { }

        [RelayCommand]
        private void Accept()
        {
            // Save acceptance
            try
            {
                var path = GetAcceptanceFilePath();
                File.WriteAllText(path, _rawVersion);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save beta acceptance: {ex.Message}");
            }

            Accepted?.Invoke();
        }

        [RelayCommand]
        private void Cancel()
        {
            // close app
            Environment.Exit(0);
        }

        public static bool IsNoticeAccepted(string currentVersion)
        {
            try
            {
                var path = GetAcceptanceFilePath();
                if (!File.Exists(path)) return false;

                var savedVersion = File.ReadAllText(path).Trim();
                
                // Normalize versions to avoid "1.1.11" vs "1.1.11.0" mismatch
                var normCurrent = NormalizeVersion(currentVersion);
                var normSaved = NormalizeVersion(savedVersion);

                return string.Equals(normSaved, normCurrent, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static string NormalizeVersion(string version)
        {
            if (string.IsNullOrWhiteSpace(version)) return version;
            try
            {
                // Try to parse as Version object to handle standard formats
                if (Version.TryParse(version, out var v))
                {
                    // Return Major.Minor.Build (ignoring Revision if 0 or irrelevant for this check)
                    // If Build is -1, default to 0
                    return $"{v.Major}.{v.Minor}.{(v.Build < 0 ? 0 : v.Build)}";
                }
            }
            catch { }
            return version.Trim();
        }

        private static string GetAcceptanceFilePath()
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OCC_Client");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            return Path.Combine(folder, "beta_accepted.txt");
        }
    }
}
