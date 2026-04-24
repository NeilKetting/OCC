using Android.App;
using Android.Content.PM;
using Avalonia;
using Avalonia.Android;
using Microsoft.Extensions.DependencyInjection;

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
            // Register native services BEFORE the app builder starts
            App.RegisterPlatformServices = services =>
            {
                services.AddSingleton<OCC.Client.Services.Interfaces.INotificationService>(new Services.AndroidNotificationService(this));
            };

            base.OnCreate(savedInstanceState);
        }

        protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
        {
            return base.CustomizeAppBuilder(builder)
                .WithInterFont();
        }
    }
}
