#nullable enable
using System;
using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Avalonia;
using Avalonia.Android;
using OCC.Mobile;

namespace OCC.Mobile.Android
{
    [Activity(
        Label = "OCC Mobile",
        Theme = "@style/MyTheme.NoActionBar",
        Icon = "@drawable/icon",
        MainLauncher = true,
        ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode,
        WindowSoftInputMode = SoftInput.AdjustResize)]
    public class MainActivity : AvaloniaMainActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            try
            {
                base.OnCreate(savedInstanceState);
                
                if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
                {
                    if (CheckSelfPermission(global::Android.Manifest.Permission.PostNotifications) != Permission.Granted)
                    {
                        RequestPermissions(new[] { global::Android.Manifest.Permission.PostNotifications }, 0);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CRITICAL STARTUP ERROR: {ex.Message}");
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
                throw;
            }
        }
    }

    [global::Android.App.ApplicationAttribute]
    public class AndroidApp : AvaloniaAndroidApplication<OCC.Mobile.App>
    {
        public AndroidApp(IntPtr handle, global::Android.Runtime.JniHandleOwnership transfer)
            : base(handle, transfer)
        {
        }
    }
}
