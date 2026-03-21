using CoreBluetooth;
using Foundation;
using Salar.BluetoothLE.Core.Enums;
using Salar.BluetoothLE.Core.Interfaces;
using Salar.BluetoothLE.Core.Models;
using System.Reactive.Subjects;

namespace Salar.BluetoothLE.iOS;

/// <summary>
/// Implements a BLE device wrapper over iOS CoreBluetooth operations.
/// </summary>
public class IosBleDevice : IBleDevice
{
    private readonly CBPeripheral _peripheral;
    private readonly CBCentralManager _centralManager;
    private readonly IosPeripheralDelegate _peripheralDelegate;
    private readonly Subject<BleDeviceState> _stateSubject = new();
    private BleDeviceState _state = BleDeviceState.Disconnected;
    private int _mtu = 23;
    private bool _disposed;

    private TaskCompletionSource<bool>? _serviceDiscoveryTcs;
    private TaskCompletionSource<bool>? _characteristicDiscoveryTcs;
    private TaskCompletionSource<byte[]>? _readTcs;
    private TaskCompletionSource<bool>? _writeTcs;
    private TaskCompletionSource<bool>? _descriptorTcs;

    private readonly Dictionary<Guid, Action<byte[]>> _notificationHandlers = new();
    private List<IBleService>? _services;

    public string Id => _peripheral.Identifier.ToString();
    public string? Name => _peripheral.Name;
    public BleDeviceState State => _state;
    public int Mtu => _mtu;
    public IObservable<BleDeviceState> StateChanged => _stateSubject;

    /// <summary>
    /// Initializes a new IosBleDevice instance.
    /// </summary>
    public IosBleDevice(CBPeripheral peripheral, CBCentralManager centralManager)
    {
        _peripheral = peripheral;
        _centralManager = centralManager;

        _peripheralDelegate = new IosPeripheralDelegate();
        _peripheralDelegate.OnDiscoveredServices += OnServicesDiscovered;
        _peripheralDelegate.OnDiscoveredCharacteristics += OnCharacteristicsDiscovered;
        _peripheralDelegate.OnUpdatedCharacteristicValue += OnCharacteristicValueUpdated;
        _peripheralDelegate.OnWroteCharacteristicValue += OnCharacteristicValueWritten;
        _peripheral.Delegate = _peripheralDelegate;
    }

    private void OnServicesDiscovered(CBPeripheral peripheral, NSError? error)
    {
        if (error != null)
            _serviceDiscoveryTcs?.TrySetException(new BleException(BleErrorCode.ServiceNotFound, error.LocalizedDescription));
        else
            _serviceDiscoveryTcs?.TrySetResult(true);
    }

    private void OnCharacteristicsDiscovered(CBPeripheral peripheral, CBService service, NSError? error)
    {
        if (error != null)
            _characteristicDiscoveryTcs?.TrySetException(new BleException(BleErrorCode.CharacteristicNotFound, error.LocalizedDescription));
        else
            _characteristicDiscoveryTcs?.TrySetResult(true);
    }

    private void OnCharacteristicValueUpdated(CBPeripheral peripheral, CBCharacteristic characteristic, NSError? error)
    {
        if (_readTcs != null)
        {
            if (error != null)
                _readTcs.TrySetException(new BleException(BleErrorCode.OperationFailed, error.LocalizedDescription));
            else
                _readTcs.TrySetResult(characteristic.Value?.ToArray() ?? Array.Empty<byte>());
        }
        else
        {
            var uuid = Guid.Parse(characteristic.UUID.ToString());
            if (_notificationHandlers.TryGetValue(uuid, out var handler))
                handler(characteristic.Value?.ToArray() ?? Array.Empty<byte>());
        }
    }

    private void OnCharacteristicValueWritten(CBPeripheral peripheral, CBCharacteristic characteristic, NSError? error)
    {
        if (error != null)
            _writeTcs?.TrySetException(new BleException(BleErrorCode.OperationFailed, error.LocalizedDescription));
        else
            _writeTcs?.TrySetResult(true);
    }

    internal void SetConnected()
    {
        _state = BleDeviceState.Connected;
        _stateSubject.OnNext(_state);
        _mtu = (int)_peripheral.GetMaximumWriteValueLength(CBCharacteristicWriteType.WithResponse);
    }

    internal void SetDisconnected()
    {
        _state = BleDeviceState.Disconnected;
        _stateSubject.OnNext(_state);
        // Clear the cached service list so that the next connection triggers
        // fresh service discovery rather than returning stale objects.
        _services = null;
    }

    internal async Task DiscoverServicesAsync(CancellationToken cancellationToken)
    {
        _serviceDiscoveryTcs = new TaskCompletionSource<bool>();
        using var reg = cancellationToken.Register(() => _serviceDiscoveryTcs.TrySetCanceled());
        _peripheral.DiscoverServices();
        await _serviceDiscoveryTcs.Task;
    }

    internal async Task DiscoverCharacteristicsAsync(CBService service, CancellationToken cancellationToken)
    {
        _characteristicDiscoveryTcs = new TaskCompletionSource<bool>();
        using var reg = cancellationToken.Register(() => _characteristicDiscoveryTcs.TrySetCanceled());
        _peripheral.DiscoverCharacteristics(service);
        await _characteristicDiscoveryTcs.Task;
    }

    internal async Task<byte[]> ReadCharacteristicAsync(CBCharacteristic characteristic, CancellationToken cancellationToken)
    {
        _readTcs = new TaskCompletionSource<byte[]>();
        using var reg = cancellationToken.Register(() => _readTcs.TrySetCanceled());
        _peripheral.ReadValue(characteristic);
        return await _readTcs.Task;
    }

    internal async Task WriteCharacteristicAsync(CBCharacteristic characteristic, byte[] data, WriteType writeType, CancellationToken cancellationToken)
    {
        var nsData = NSData.FromArray(data);
        var cbWriteType = writeType == WriteType.WithoutResponse
            ? CBCharacteristicWriteType.WithoutResponse
            : CBCharacteristicWriteType.WithResponse;

        if (writeType == WriteType.WithResponse)
        {
            _writeTcs = new TaskCompletionSource<bool>();
            using var reg = cancellationToken.Register(() => _writeTcs.TrySetCanceled());
            _peripheral.WriteValue(nsData, characteristic, cbWriteType);
            await _writeTcs.Task;
        }
        else
        {
            _peripheral.WriteValue(nsData, characteristic, cbWriteType);
        }
    }

    internal async Task SetNotificationAsync(CBCharacteristic characteristic, bool enable, Action<byte[]>? handler, CancellationToken cancellationToken)
    {
        var uuid = Guid.Parse(characteristic.UUID.ToString());
        _peripheral.SetNotifyValue(enable, characteristic);

        if (enable && handler != null)
            _notificationHandlers[uuid] = handler;
        else
            _notificationHandlers.Remove(uuid);

        await Task.CompletedTask;
    }

    /// <summary>
    /// Gets the GATT services exposed by this device.
    /// </summary>
    public async Task<IReadOnlyList<IBleService>> GetServicesAsync(CancellationToken cancellationToken = default)
    {
        if (_services == null)
        {
            await DiscoverServicesAsync(cancellationToken);
            _services = _peripheral.Services?
                .Select(s => (IBleService)new IosBleService(s, this))
                .ToList() ?? new List<IBleService>();
        }
        return _services.AsReadOnly();
    }

    /// <summary>
    /// Gets the service with the specified UUID.
    /// </summary>
    public async Task<IBleService?> GetServiceAsync(Guid serviceUuid, CancellationToken cancellationToken = default)
    {
        var services = await GetServicesAsync(cancellationToken);
        return services.FirstOrDefault(s => s.Uuid == serviceUuid);
    }

    /// <summary>
    /// Disconnects from the BLE device.
    /// </summary>
    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        // Signal disconnecting state immediately so callers see the transition.
        // The actual disconnection is asynchronous — the IosBleAdapter's
        // OnDisconnectedPeripheral callback fires SetDisconnected() when Core
        // Bluetooth confirms the peripheral is gone.
        _state = BleDeviceState.Disconnecting;
        _stateSubject.OnNext(_state);
        _centralManager.CancelPeripheralConnection(_peripheral);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Requests the specified MTU for the BLE connection.
    /// </summary>
    public Task<int> RequestMtuAsync(int mtu, CancellationToken cancellationToken = default)
    {
        _mtu = (int)_peripheral.GetMaximumWriteValueLength(CBCharacteristicWriteType.WithResponse);
        return Task.FromResult(_mtu);
    }

    /// <summary>
    /// Releases the device and any native iOS Bluetooth resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _stateSubject.OnCompleted();
        _stateSubject.Dispose();
    }
}
