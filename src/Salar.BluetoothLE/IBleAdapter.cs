using Salar.BluetoothLE.Core.Enums;
using Salar.BluetoothLE.Core.Interfaces;
using Salar.BluetoothLE.Core.Models;

namespace Salar.BluetoothLE;

/// <summary>
/// Defines the cross-platform BLE adapter operations for scanning and connecting to devices.
/// </summary>
public interface IBleAdapter
{
    BleAdapterState AdapterState { get; }
    BleLibraryState LibraryState { get; }
    IReadOnlyList<IBleDevice> ConnectedDevices { get; }

    IObservable<BleAdapterState> AdapterStateChanged { get; }
    IObservable<ScanResult> ScanResultReceived { get; }
    IObservable<BleLibraryState> LibraryStateChanged { get; }

    /// <summary>
    /// Requests Bluetooth access for the current platform.
    /// </summary>
    Task<BlePermissionStatus> RequestAccessAsync(CancellationToken cancellationToken = default);
    /// <summary>
    /// Starts scanning for nearby BLE devices.
    /// </summary>
    Task StartScanAsync(ScanConfig? config = null, CancellationToken cancellationToken = default);
    /// <summary>
    /// Stops any active BLE scan.
    /// </summary>
    Task StopScanAsync(CancellationToken cancellationToken = default);
    /// <summary>
    /// Connects to the BLE device with the specified address.
    /// </summary>
    Task<IBleDevice> ConnectAsync(string address, ConnectionConfig? config = null, CancellationToken cancellationToken = default);
    /// <summary>
    /// Reconnects to a previously known BLE device.
    /// </summary>
    Task<IBleDevice> ReconnectAsync(string address, ConnectionConfig? config = null, CancellationToken cancellationToken = default);
}
