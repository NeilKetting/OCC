using Android.App;
using Android.Content.PM;
using Avalonia;
using Avalonia.Android;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Runtime.Versioning;
using Firebase.Messaging;
using Android.Gms.Tasks;
using OCC.Mobile.Features.Notifications;


namespace OCC.Client.Android
{
    [Activity(
        Label = "OCC.Client.Android",
        Theme = "@style/MyTheme.NoActionBar",
        Icon = "@drawable/occ_app_icon",
        MainLauncher = true,
        ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
    public class MainActivity : AvaloniaMainActivity<App>
    {
        protected override void OnCreate(global::Android.OS.Bundle? savedInstanceState)
        {
            try 
            {
                /*
                // Register native services BEFORE the app builder starts
                App.RegisterPlatformServices = services =>
                {
                    services.AddSingleton<OCC.Client.Services.Interfaces.INotificationService>(new Services.AndroidNotificationService(this.ApplicationContext));
                };
                */

                // Initialize the SplashScreen API (required for modern Android themes)
                // AndroidX.Core.SplashScreen.SplashScreen.InstallSplashScreen(this);

                base.OnCreate(savedInstanceState);

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
                System.Diagnostics.Debug.WriteLine($"CRITICAL ERROR IN ONCREATE: {ex.Message}");
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
                    var pushService = App.Services?.GetService<IPushNotificationService>();
                    if (pushService != null)
                    {
                        pushService.UpdateToken(token);
                    }
                    System.Diagnostics.Debug.WriteLine($"[Firebase] Initial Token: {token}");
                }
            }
        }

        protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
        {
            return base.CustomizeAppBuilder(builder)
                .WithInterFont();
        }
    }
}
