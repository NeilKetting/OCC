using System;
using System.IO;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Net;
using Android.OS;
using AndroidX.Core.Content;
using OCC.Mobile.Services;
using Application = Android.App.Application;

namespace OCC.Mobile.Android.Services
{
    public class AndroidAppInstaller : IAppInstaller
    {
        private readonly Context _context;

        public AndroidAppInstaller()
        {
            _context = Application.Context;
        }

        private Context GetBestContext()
        {
            return MainActivity.Instance ?? _context;
        }

        public async Task<bool> InstallPackageAsync(string localPath)
        {
            try
            {
                var bestContext = GetBestContext();

                if (!File.Exists(localPath))
                {
                    global::Android.Widget.Toast.MakeText(bestContext, "Error: APK download file not found!", global::Android.Widget.ToastLength.Long)?.Show();
                    return false;
                }

                // Android 8.0 (API 26+) requires explicit 'Install unknown apps' permission per app
#pragma warning disable CA1416
                if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                {
                    var packageManager = bestContext.PackageManager;
                    if (packageManager != null && !packageManager.CanRequestPackageInstalls())
                    {
                        global::Android.Widget.Toast.MakeText(
                            bestContext,
                            "Please enable 'Allow from this source' for OCC Field Hub in Settings, then tap Update again.",
                            global::Android.Widget.ToastLength.Long)?.Show();

                        try
                        {
                            var settingsIntent = new Intent(global::Android.Provider.Settings.ActionManageUnknownAppSources);
                            settingsIntent.SetData(global::Android.Net.Uri.Parse($"package:{bestContext.PackageName}"));
                            settingsIntent.AddFlags(ActivityFlags.NewTask);
                            bestContext.StartActivity(settingsIntent);
                        }
                        catch (Exception settingsEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to launch unknown sources settings: {settingsEx.Message}");
                        }

                        return false;
                    }
                }
#pragma warning restore CA1416

                global::Android.Widget.Toast.MakeText(bestContext, "Triggering System Installer...", global::Android.Widget.ToastLength.Short)?.Show();

                // Copy to External Cache or Cache for maximal system file provider accessibility
                var targetDir = bestContext.ExternalCacheDir ?? bestContext.CacheDir;
                if (targetDir == null)
                {
                    global::Android.Widget.Toast.MakeText(bestContext, "Error: Cache directory inaccessible!", global::Android.Widget.ToastLength.Long)?.Show();
                    return false;
                }

                var externalPath = Path.Combine(targetDir.AbsolutePath, "update_install.apk");
                
                if (File.Exists(externalPath)) File.Delete(externalPath);
                using (var source = File.OpenRead(localPath))
                using (var destination = File.Create(externalPath))
                {
                    await source.CopyToAsync(destination);
                }

                var file = new Java.IO.File(externalPath);
                // Authority matches AndroidManifest.xml provider
                var apkUri = FileProvider.GetUriForFile(bestContext, "com.occ.fieldhub.fileprovider", file);
                
                var intent = new Intent(Intent.ActionView);
                intent.SetDataAndType(apkUri, "application/vnd.android.package-archive");
                intent.AddFlags(ActivityFlags.GrantReadUriPermission);
                intent.AddFlags(ActivityFlags.GrantWriteUriPermission);
                intent.AddFlags(ActivityFlags.ClearTop);
                intent.AddFlags(ActivityFlags.NewTask);
                
                // Extra metadata to help system installer resolution
                intent.PutExtra(Intent.ExtraNotUnknownSource, true);
                intent.PutExtra(Intent.ExtraReturnResult, true);

                bestContext.StartActivity(intent);
                return true;
            }
            catch (Exception ex)
            {
                var bestContext = GetBestContext();
                global::Android.Widget.Toast.MakeText(bestContext, $"Fatal Install Error: {ex.Message}", global::Android.Widget.ToastLength.Long)?.Show();
                System.Diagnostics.Debug.WriteLine($"Install failed: {ex.Message}");
                return false;
            }
        }

        public Task ShowToastAsync(string message)
        {
            var bestContext = GetBestContext();
            global::Android.Widget.Toast.MakeText(bestContext, message, global::Android.Widget.ToastLength.Short)?.Show();
            return Task.CompletedTask;
        }
    }
}
