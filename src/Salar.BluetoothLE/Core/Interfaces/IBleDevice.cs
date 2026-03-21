using Salar.BluetoothLE.Core.Enums;

namespace Salar.BluetoothLE.Core.Interfaces;

/// <summary>
/// Represents a BLE peripheral and its GATT operations.
/// </summary>
public interface IBleDevice : IDisposable
{
    string Id { get; }
    string? Name { get; }
    BleDeviceState State { get; }
    int Mtu { get; }

    IObservable<BleDeviceState> StateChanged { get; }

    /// <summary>
    /// Gets the GATT services exposed by this device.
    /// </summary>
    Task<IReadOnlyList<IBleService>> GetServicesAsync(CancellationToken cancellationToken = default);
    /// <summary>
    /// Gets the service with the specified UUID.
    /// </summary>
    Task<IBleService?> GetServiceAsync(Guid serviceUuid, CancellationToken cancellationToken = default);
    /// <summary>
    /// Disconnects from the BLE device.
    /// </summary>
    Task DisconnectAsync(CancellationToken cancellationToken = default);
    /// <summary>
    /// Requests the specified MTU for the BLE connection.
    /// </summary>
    Task<int> RequestMtuAsync(int mtu, CancellationToken cancellationToken = default);
}
