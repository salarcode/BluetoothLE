using Salar.BluetoothLE.Core.Enums;
using Salar.BluetoothLE.Core.Interfaces;
using Salar.BluetoothLE.Core.Models;
using Salar.BluetoothLE.Linux.BlueZ;
using System.Reactive.Subjects;

namespace Salar.BluetoothLE.Linux;

/// <summary>
/// Implements a BLE device wrapper over Linux BlueZ GATT operations.
/// </summary>
public sealed class LinuxBleDevice : IBleDevice
{
    private readonly Device _device;
    private readonly Action<LinuxBleDevice> _disconnectCallback;
    private readonly Subject<BleDeviceState> _stateSubject = new();

    private readonly object _stateLock = new();
    private List<IBleService>? _services;
    private string? _name;
    private BleDeviceState _state = BleDeviceState.Disconnected;
    private int _mtu = 23;
    private bool _disposed;

    public string Id { get; }
    public string? Name => _name;
    public BleDeviceState State => _state;
    public int Mtu => _mtu;
    public IObservable<BleDeviceState> StateChanged => _stateSubject;

    /// <summary>
    /// Initializes a new LinuxBleDevice instance.
    /// </summary>
    internal LinuxBleDevice(Device device, Action<LinuxBleDevice> disconnectCallback, string address)
    {
        _device = device;
        _disconnectCallback = disconnectCallback;
        Id = address;

        _device.Connected += OnConnectedAsync;
        _device.Disconnected += OnDisconnectedAsync;
    }

    internal async Task ConnectInternalAsync(ConnectionConfig config, CancellationToken cancellationToken)
    {
        SetState(BleDeviceState.Connecting);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(config.ConnectionTimeout);

        try
        {
            var properties = await _device.GetPropertiesAsync().WaitAsync(timeoutCts.Token);
            _name = string.IsNullOrWhiteSpace(properties.Alias) ? properties.Name : properties.Alias;

            if (!properties.IsConnected)
            {
                await _device.ConnectAsync().WaitAsync(timeoutCts.Token);
                await _device.WaitForPropertyValueAsync("Connected", true, config.ConnectionTimeout).WaitAsync(timeoutCts.Token);
            }

            if (!properties.ServicesResolved)
                await _device.WaitForPropertyValueAsync("ServicesResolved", true, config.ConnectionTimeout).WaitAsync(timeoutCts.Token);

            SetState(BleDeviceState.Connected);
        }
        catch (OperationCanceledException)
        {
            SetState(BleDeviceState.Failed);
            throw new BleException(BleErrorCode.ConnectionTimeout);
        }
        catch (BleException)
        {
            SetState(BleDeviceState.Failed);
            throw;
        }
        catch (Exception ex)
        {
            SetState(BleDeviceState.Failed);
            throw new BleException(BleErrorCode.ConnectionFailed, ex.Message, ex);
        }
    }

    /// <summary>
    /// Gets the GATT services exposed by this device.
    /// </summary>
    public async Task<IReadOnlyList<IBleService>> GetServicesAsync(CancellationToken cancellationToken = default)
    {
        if (State != BleDeviceState.Connected)
            throw new BleException(BleErrorCode.NotConnected);

        if (_services != null)
            return _services.AsReadOnly();

        var services = await _device.GetServicesAsync().WaitAsync(cancellationToken);
        var wrapped = new List<IBleService>(services.Count);
        foreach (var service in services)
        {
            var properties = await service.GetAllAsync().WaitAsync(cancellationToken);
            wrapped.Add(new LinuxBleService(service, Guid.Parse(BlueZManager.NormalizeUUID(properties.UUID))));
        }

        _services = wrapped;
        return _services.AsReadOnly();
    }

    /// <summary>
    /// Gets the service with the specified UUID.
    /// </summary>
    public async Task<IBleService?> GetServiceAsync(Guid serviceUuid, CancellationToken cancellationToken = default)
    {
        var service = await _device.GetServiceAsync(BlueZManager.NormalizeUUID(serviceUuid.ToString())).WaitAsync(cancellationToken);
        return service == null
            ? null
            : new LinuxBleService(service, serviceUuid);
    }

    /// <summary>
    /// Disconnects from the BLE device.
    /// </summary>
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (State == BleDeviceState.Disconnected)
            return;

        SetState(BleDeviceState.Disconnecting);

        try
        {
            await _device.DisconnectAsync().WaitAsync(cancellationToken);
        }
        finally
        {
            DisposeServices();
            SetState(BleDeviceState.Disconnected);
            _disconnectCallback(this);
        }
    }

    /// <summary>
    /// Requests the specified MTU for the BLE connection.
    /// </summary>
    public Task<int> RequestMtuAsync(int mtu, CancellationToken cancellationToken = default)
        => Task.FromResult(_mtu);

    private Task OnConnectedAsync(Device sender, BlueZEventArgs eventArgs)
    {
        SetState(BleDeviceState.Connected);
        return Task.CompletedTask;
    }

    private Task OnDisconnectedAsync(Device sender, BlueZEventArgs eventArgs)
    {
        DisposeServices();
        SetState(BleDeviceState.Disconnected);
        _disconnectCallback(this);
        return Task.CompletedTask;
    }

    private void SetState(BleDeviceState state)
    {
        lock (_stateLock)
        {
            if (_state == state)
                return;

            _state = state;
            _stateSubject.OnNext(state);
        }
    }

    private void DisposeServices()
    {
        if (_services == null)
            return;

        foreach (var service in _services)
            (service as IDisposable)?.Dispose();
        _services = null;
    }

    /// <summary>
    /// Releases the device and any underlying Linux Bluetooth resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        _device.Connected -= OnConnectedAsync;
        _device.Disconnected -= OnDisconnectedAsync;

        DisposeServices();
        _device.Dispose();

        _stateSubject.OnCompleted();
        _stateSubject.Dispose();
    }
}
