#nullable enable
using System;
using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Avalonia;
using Avalonia.Android;
using OCC.Mobile;
using OCC.Mobile.Android.Services;
using OCC.Mobile.Services;
using Microsoft.Extensions.DependencyInjection;
using Firebase.Messaging;
using Android.Gms.Tasks;
using OCC.Mobile.Features.Notifications;


namespace OCC.Mobile.Android
{
    [Activity(
        Label = "OCC Field Hub",
        Theme = "@style/MyTheme.NoActionBar",
        Icon = "@drawable/icon",
        MainLauncher = true,
        ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode,
        WindowSoftInputMode = SoftInput.AdjustResize,
        Exported = true)]
    public class MainActivity : AvaloniaMainActivity
    {
        public static MainActivity? Instance { get; private set; }

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            Instance = this;
            try
            {
                base.OnCreate(savedInstanceState);
                
#pragma warning disable CA1416
                if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
                {
                    if (CheckSelfPermission(global::Android.Manifest.Permission.PostNotifications) != Permission.Granted)
                    {
                        RequestPermissions(new[] { global::Android.Manifest.Permission.PostNotifications }, 0);
                    }
                }
#pragma warning restore CA1416
                
                // Fetch and register FCM token on startup
                try
                {
                    FirebaseMessaging.Instance.GetToken().AddOnCompleteListener(new TokenCompleteListener());
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Firebase] Error fetching token: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CRITICAL STARTUP ERROR: {ex.Message}");
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
                throw;
            }
        }

        private class TokenCompleteListener : Java.Lang.Object, IOnCompleteListener
        {
            public void OnComplete(Task task)
            {
                if (task.IsSuccessful)
                {
                    var token = task.Result.ToString();
                    System.Diagnostics.Debug.WriteLine($"[Firebase] Initial Token: {token}");

                    // Wait for services to be ready
                    System.Threading.Tasks.Task.Run(async () =>
                    {
                        int retries = 0;
                        while (App.Services == null && retries < 20)
                        {
                            await System.Threading.Tasks.Task.Delay(1000);
                            retries++;
                        }

                        var pushService = App.Services?.GetService<IPushNotificationService>();
                        if (pushService != null)
                        {
                            pushService.UpdateToken(token);
                        }
                    });
                }
            }
        }
    }

    [global::Android.App.ApplicationAttribute]
    public class AndroidApp : AvaloniaAndroidApplication<OCC.Mobile.App>
    {
        static AndroidApp()
        {
            // Register Android-specific services the VERY moment the app process starts
            OCC.Mobile.App.RegisterPlatformServices = services =>
            {
                services.AddSingleton<IAppInstaller, AndroidAppInstaller>();
            };
        }

        public AndroidApp(IntPtr handle, global::Android.Runtime.JniHandleOwnership transfer)
            : base(handle, transfer)
        {
        }

        public override void OnCreate()
        {
            base.OnCreate();
            
            // Get the actual version from the Android Package Manager
            var packageInfo = PackageManager?.GetPackageInfo(PackageName ?? "", 0);
            var version = packageInfo?.VersionName ?? "1.0.0";
            
            // Set the static app version
            OCC.Mobile.App.AppVersion = version;
        }
    }
}
