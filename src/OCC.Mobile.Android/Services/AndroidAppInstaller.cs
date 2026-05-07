using System;
using System.IO;
using System.Threading.Tasks;
using Android.Content;
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

        public Task InstallPackageAsync(string localPath)
        {
            try
            {
                var file = new Java.IO.File(localPath);
                if (!file.Exists())
                {
                    throw new FileNotFoundException("APK file not found.", localPath);
                }

                var apkUri = FileProvider.GetUriForFile(_context, _context.PackageName + ".fileprovider", file);
                
                var intent = new Intent(Intent.ActionView);
                intent.SetDataAndType(apkUri, "application/vnd.android.package-archive");
                intent.AddFlags(ActivityFlags.NewTask);
                intent.AddFlags(ActivityFlags.GrantReadUriPermission);
                intent.AddFlags(ActivityFlags.ClearTop);

                _context.StartActivity(intent);
                
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                return Task.FromException(ex);
            }
        }
    }
}
