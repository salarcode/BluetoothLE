using Salar.BluetoothLE.Core.Enums;
using Salar.BluetoothLE.Core.Interfaces;
using Salar.BluetoothLE.Core.Models;

namespace Salar.BluetoothLE;

public interface IBleAdapter
{
    BleAdapterState AdapterState { get; }
    BleLibraryState LibraryState { get; }
    IReadOnlyList<IBleDevice> ConnectedDevices { get; }

    IObservable<BleAdapterState> AdapterStateChanged { get; }
    IObservable<ScanResult> ScanResultReceived { get; }
    IObservable<BleLibraryState> LibraryStateChanged { get; }

    Task<BlePermissionStatus> RequestAccessAsync(CancellationToken cancellationToken = default);
    Task StartScanAsync(ScanConfig? config = null, CancellationToken cancellationToken = default);
    Task StopScanAsync(CancellationToken cancellationToken = default);
    Task<IBleDevice> ConnectAsync(string address, ConnectionConfig? config = null, CancellationToken cancellationToken = default);
    Task<IBleDevice> ReconnectAsync(string address, ConnectionConfig? config = null, CancellationToken cancellationToken = default);
}
