using Salar.BluetoothLE.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Salar.BluetoothLE.Maui;

public static class BleServiceCollectionExtensions
{
    /// <summary>
    /// Registers BLE services with a custom adapter factory.
    /// </summary>
    public static IServiceCollection AddBluetoothLE(this IServiceCollection services, Func<IServiceProvider, IBleAdapter> adapterFactory)
    {
        ArgumentNullException.ThrowIfNull(adapterFactory);
        services.AddSingleton(adapterFactory);
        return services;
    }

    /// <summary>
    /// Registers BLE services with a pre-constructed adapter instance.
    /// </summary>
    public static IServiceCollection AddBluetoothLE(this IServiceCollection services, IBleAdapter adapter)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        services.AddSingleton(adapter);
        return services;
    }
}
