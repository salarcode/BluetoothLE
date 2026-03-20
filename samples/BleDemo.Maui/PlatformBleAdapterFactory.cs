using Salar.BluetoothLE.Core.Interfaces;

#if ANDROID
using Android.App;
using Salar.BluetoothLE.Android;
using Application = Android.App.Application;
#elif IOS
using Salar.BluetoothLE.iOS;
#elif WINDOWS
using Salar.BluetoothLE.Windows;
#endif

namespace BleDemo.Maui;

internal static class PlatformBleAdapterFactory
{
    public static IBleAdapter Create() =>
#if ANDROID
        new AndroidBleAdapter(Application.Context);
#elif IOS
        new IosBleAdapter();
#elif WINDOWS
        new WindowsBleAdapter();
#else
        throw new PlatformNotSupportedException("BleDemo.Maui requires a supported BLE platform.");
#endif
}
