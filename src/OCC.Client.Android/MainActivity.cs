using Android.App;
using Android.Content.PM;
using Avalonia;
using Avalonia.Android;

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
        protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
        {
            return base.CustomizeAppBuilder(builder)
                .WithInterFont();
        }
    }
}
