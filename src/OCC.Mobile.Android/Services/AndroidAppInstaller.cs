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

        public async Task InstallPackageAsync(string localPath)
        {
            try
            {
                var bestContext = GetBestContext();
                global::Android.Widget.Toast.MakeText(bestContext, "Triggering System Installer...", global::Android.Widget.ToastLength.Short)?.Show();

                if (!File.Exists(localPath))
                {
                    global::Android.Widget.Toast.MakeText(bestContext, "Error: APK not found!", global::Android.Widget.ToastLength.Long)?.Show();
                    return;
                }

                // Copy to External Cache for maximal system accessibility
                var externalDir = bestContext.ExternalCacheDir;
                var externalPath = Path.Combine(externalDir!.AbsolutePath, "update_install.apk");
                
                if (File.Exists(externalPath)) File.Delete(externalPath);
                using (var source = File.OpenRead(localPath))
                using (var destination = File.Create(externalPath))
                {
                    await source.CopyToAsync(destination);
                }

                var file = new Java.IO.File(externalPath);
                // Authority is now all-lowercase and standardized
                var apkUri = FileProvider.GetUriForFile(bestContext, "com.occ.fieldhub.fileprovider", file);
                
                var intent = new Intent(Intent.ActionView);
                intent.SetDataAndType(apkUri, "application/vnd.android.package-archive");
                
                if (bestContext is not Activity)
                {
                    intent.AddFlags(ActivityFlags.NewTask);
                }
                
                intent.AddFlags(ActivityFlags.GrantReadUriPermission);
                intent.AddFlags(ActivityFlags.GrantWriteUriPermission);
                intent.AddFlags(ActivityFlags.ClearTop);
                
                // Extra metadata to help the installer
                intent.PutExtra(Intent.ExtraNotUnknownSource, true);
                intent.PutExtra(Intent.ExtraReturnResult, true);

                bestContext.StartActivity(intent);
            }
            catch (Exception ex)
            {
                var bestContext = GetBestContext();
                global::Android.Widget.Toast.MakeText(bestContext, $"Fatal Install Error: {ex.Message}", global::Android.Widget.ToastLength.Long)?.Show();
                System.Diagnostics.Debug.WriteLine($"Install failed: {ex.Message}");
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
