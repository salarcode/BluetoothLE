using Android.App;
using Android.Content.PM;
using Android.OS;
using Avalonia;
using Avalonia.Android;
using Salar.BluetoothLE.Android;

namespace BleAvaloniaDemo.Android
{
    [Activity(
        Label = "BleAvaloniaDemo.Android",
        Theme = "@style/MyTheme.NoActionBar",
        Icon = "@drawable/icon",
        MainLauncher = true,
        ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
    public class MainActivity : AvaloniaMainActivity<App>
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            BleDemoPlatformServices.Initialize(
                static () => new AndroidBleAdapter(global::Android.App.Application.Context),
                () => AndroidPermissionService.RequestBluetoothAccessAsync(this));

            base.OnCreate(savedInstanceState);
        }

        protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
        {
            return base.CustomizeAppBuilder(builder)
                .WithInterFont();
        }

        public override void OnRequestPermissionsResult(int requestCode, string[]? permissions, Permission[]? grantResults)
        {
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
            AndroidPermissionService.CompleteRequest(requestCode, permissions, grantResults);
        }
    }
}
