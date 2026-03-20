using Salar.BluetoothLE.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Salar.BluetoothLE.Maui;

public static class MauiBleExtensions
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
    {
        throw new PlatformNotSupportedException(
            "Salar.BluetoothLE requires a platform-specific adapter. " +
            "Use AddBluetoothLE() overload with a platform adapter factory, " +
            "or reference the platform-specific package (Salar.BluetoothLE.Android, Salar.BluetoothLE.iOS, Salar.BluetoothLE.Windows).");
    }
}
