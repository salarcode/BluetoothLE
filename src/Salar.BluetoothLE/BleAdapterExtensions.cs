using Microsoft.Extensions.DependencyInjection;

namespace Salar.BluetoothLE;

/// <summary>
/// Provides dependency injection helpers for registering BLE services.
/// </summary>
public static class BleAdapterExtensions
{
    /// <summary>
    /// Registers the BLE adapter and related services for the current platform.
    /// Call this in your MauiProgram.cs within CreateMauiApp().
    /// </summary>
    public static IServiceCollection AddBluetoothLE(this IServiceCollection services)
    {
        services.AddSingleton<IBleAdapter>(sp =>
        {
            return CreatePlatformAdapter();
        });
        return services;
    }

    private static IBleAdapter CreatePlatformAdapter()
        =>
#if ANDROID
        new Android.AndroidBleAdapter(Application.Context);
#elif IOS
        new iOS.IosBleAdapter();
#elif WINDOWS
        new Windows.WindowsBleAdapter();
#else
        throw new PlatformNotSupportedException("Salar.BluetoothLE requires a supported BLE platform.");
#endif
}
