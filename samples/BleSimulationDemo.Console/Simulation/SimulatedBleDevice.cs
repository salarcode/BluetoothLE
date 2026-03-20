using System.Reactive.Subjects;
using Salar.BluetoothLE.Core.Enums;
using Salar.BluetoothLE.Core.Interfaces;

namespace BleDemo.Console.Simulation;

internal sealed class SimulatedBleDevice : IBleDevice
{
    private readonly Subject<BleDeviceState> _stateSubject = new();
    private readonly Random _random;
    private BleDeviceState _state = BleDeviceState.Connected;
    private bool _disposed;

    public string Id { get; }
    public string? Name { get; }
    public int Mtu { get; private set; } = 23;

    // The last known RSSI; drifts slightly on each read.
    private int _rssi;
    public int Rssi => _rssi + _random.Next(-3, 4);

    public BleDeviceState State
    {
        get => _state;
        private set
        {
            if (_state == value) return;
            _state = value;
            _stateSubject.OnNext(value);
        }
    }

    public IObservable<BleDeviceState> StateChanged => _stateSubject;

    public SimulatedBleDevice(string id, string? name, int rssi, Random random)
    {
        Id      = id;
        Name    = name;
        _rssi   = rssi;
        _random = random;
    }

    public Task<IReadOnlyList<IBleService>> GetServicesAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<IBleService> services = new List<IBleService>
        {
            new SimulatedBleService(Guid.Parse("0000180D-0000-1000-8000-00805F9B34FB"), "Heart Rate"),
            new SimulatedBleService(Guid.Parse("00001800-0000-1000-8000-00805F9B34FB"), "Generic Access"),
        };
        return Task.FromResult(services);
    }

    public Task<IBleService?> GetServiceAsync(Guid serviceUuid, CancellationToken cancellationToken = default)
    {
        IBleService? svc = new SimulatedBleService(serviceUuid, "Simulated Service");
        return Task.FromResult<IBleService?>(svc);
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        State = BleDeviceState.Disconnecting;
        await Task.Delay(500, cancellationToken);
        State = BleDeviceState.Disconnected;
    }

    public async Task<int> RequestMtuAsync(int mtu, CancellationToken cancellationToken = default)
    {
        await Task.Delay(200, cancellationToken);
        Mtu = Math.Min(mtu, 517);
        return Mtu;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _stateSubject.OnCompleted();
        _stateSubject.Dispose();
        _disposed = true;
    }
}
