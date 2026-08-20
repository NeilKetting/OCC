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
using Firebase;
using Android.Gms.Tasks;
using OCC.Mobile.Features.Notifications;


namespace OCC.Mobile.Android
{
    [Activity(
        Label = "OCC Field Hub",
        Theme = "@style/MyTheme.NoActionBar",
        Icon = "@mipmap/occ_branded_icon",
        MainLauncher = true,
        ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.SmallestScreenSize | ConfigChanges.ScreenLayout | ConfigChanges.UiMode | ConfigChanges.Density,
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

                // Explicitly Initialize Firebase with Hardcoded Fallback
                try {
                    var options = new FirebaseOptions.Builder()
                        .SetApiKey("AIzaSyB2921Ya78f6R0PhdwbYIK9-xvsB0DhWAs")
                        .SetApplicationId("1:252587602101:android:525ad027c3ff90c05acefc")
                        .SetProjectId("occ-erp")
                        .SetGcmSenderId("252587602101")
                        .Build();
                    
                    Firebase.FirebaseApp.InitializeApp(this, options);
                    System.Diagnostics.Debug.WriteLine("[Firebase] Hardcoded initialization successful.");
                } catch (Exception ex) {
                    System.Diagnostics.Debug.WriteLine($"[Firebase] Initialization error: {ex.Message}");
                    var pushService = App.Services?.GetService<IPushNotificationService>();
                    if (pushService is Features.Notifications.PushNotificationService pns)
                    {
                         pns.UpdateStatus($"Error: Firebase Init {ex.Message}");
                    }
                }

                // Check for Google Play Services and show specific error if failed
                var availability = global::Android.Gms.Common.GoogleApiAvailability.Instance.IsGooglePlayServicesAvailable(this);
                if (availability != global::Android.Gms.Common.ConnectionResult.Success)
                {
                    var pushService = App.Services?.GetService<IPushNotificationService>();
                    if (pushService is Features.Notifications.PushNotificationService pns)
                    {
                         System.Threading.Tasks.Task.Run(() => pns.UpdateStatus($"Error: Play Services {availability}"));
                    }
                    System.Diagnostics.Debug.WriteLine($"[Firebase] Google Play Services not available: {availability}");
                }
                
                // Aggressive Force-Fetch for FCM token
                System.Threading.Tasks.Task.Run(async () => {
                    int attempts = 0;
                    while (attempts < 5) {
                        try {
                            var pushService = App.Services?.GetService<IPushNotificationService>();
                            if (pushService is Features.Notifications.PushNotificationService pns)
                            {
                                pns.UpdateStatus($"Requesting Token (Attempt {attempts+1})...");
                            }

                            FirebaseMessaging.Instance.GetToken().AddOnCompleteListener(new TokenCompleteListener());
                            
                            // Give it 15 seconds per attempt
                            await System.Threading.Tasks.Task.Delay(15000);
                            
                            if (pushService != null && pushService.Status == "Registered Successfully") break;
                            
                            attempts++;
                        } catch (Exception ex) {
                            System.Diagnostics.Debug.WriteLine($"[Firebase] Fetch Attempt {attempts} failed: {ex.Message}");
                            await System.Threading.Tasks.Task.Delay(3000);
                            attempts++;
                        }
                    }
                    
                    var finalService = App.Services?.GetService<IPushNotificationService>();
                    if (finalService != null && !finalService.Status.Contains("Successfully")) {
                        ((Features.Notifications.PushNotificationService)finalService).UpdateStatus("Error: Firebase Timeout");
                    }
                });
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
            // Register Android Mono-VM unhandled exception handler
            global::Android.Runtime.AndroidEnvironment.UnhandledExceptionRaiser += (sender, e) =>
            {
                if (e.Exception != null)
                {
                    OCC.Mobile.Infrastructure.CrashDetector.HandleCrash(e.Exception, "AndroidEnvironment.UnhandledExceptionRaiser");
                }
            };

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
            try
            {
                // Set the static app version BEFORE base.OnCreate() initializes Avalonia & ViewModels
                var packageInfo = PackageManager?.GetPackageInfo(PackageName ?? "", 0);
                var version = packageInfo?.VersionName;
                if (!string.IsNullOrEmpty(version))
                {
                    OCC.Mobile.App.AppVersion = version;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AndroidApp] Error reading PackageInfo version: {ex.Message}");
            }

            base.OnCreate();
        }
    }
}
