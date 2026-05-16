using Android.App;
using Android.Content.PM;
using Avalonia;
using Avalonia.Android;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Runtime.Versioning;

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
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CRITICAL ERROR IN ONCREATE: {ex.Message}");
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
                throw;
            }
        }

        protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
        {
            return base.CustomizeAppBuilder(builder)
                .WithInterFont();
        }
    }
}
