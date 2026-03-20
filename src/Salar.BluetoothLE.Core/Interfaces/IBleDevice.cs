using Salar.BluetoothLE.Core.Enums;

namespace Salar.BluetoothLE.Core.Interfaces;

public interface IBleDevice : IDisposable
{
    string Id { get; }
    string? Name { get; }
    BleDeviceState State { get; }
    int Mtu { get; }

    IObservable<BleDeviceState> StateChanged { get; }

    Task<IReadOnlyList<IBleService>> GetServicesAsync(CancellationToken cancellationToken = default);
    Task<IBleService?> GetServiceAsync(Guid serviceUuid, CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);
    Task<int> RequestMtuAsync(int mtu, CancellationToken cancellationToken = default);
}
