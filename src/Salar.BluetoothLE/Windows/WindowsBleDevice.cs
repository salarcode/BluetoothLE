using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Salar.BluetoothLE.Core.Enums;
using Salar.BluetoothLE.Core.Interfaces;
using Salar.BluetoothLE.Core.Models;
using System.Reactive.Subjects;

namespace Salar.BluetoothLE.Windows;

public class WindowsBleDevice : IBleDevice
{
    private BluetoothLEDevice? _nativeDevice;
    private readonly ulong _bluetoothAddress;
    private readonly Subject<BleDeviceState> _stateSubject = new();
    private BleDeviceState _state = BleDeviceState.Disconnected;
    private int _mtu = 23;
    private bool _disposed;
    private List<IBleService>? _services;
    private GattSession? _gattSession;

    public string Id => _bluetoothAddress.ToString("X");
    public string? Name => _nativeDevice?.Name;
    public BleDeviceState State => _state;
    public int Mtu => _mtu;
    public IObservable<BleDeviceState> StateChanged => _stateSubject;

    public WindowsBleDevice(ulong bluetoothAddress)
    {
        _bluetoothAddress = bluetoothAddress;
    }

    internal async Task ConnectInternalAsync(ConnectionConfig config, CancellationToken cancellationToken)
    {
        _state = BleDeviceState.Connecting;
        _stateSubject.OnNext(_state);

        var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(config.ConnectionTimeout);

        try
        {
            _nativeDevice = await BluetoothLEDevice.FromBluetoothAddressAsync(_bluetoothAddress).AsTask(timeoutCts.Token);

            if (_nativeDevice == null)
                throw new BleException(BleErrorCode.ConnectionFailed, "Could not create BluetoothLEDevice.");

            _nativeDevice.ConnectionStatusChanged += OnConnectionStatusChanged;

            var servicesResult = await _nativeDevice.GetGattServicesAsync().AsTask(timeoutCts.Token);
            if (servicesResult.Status != GattCommunicationStatus.Success)
                throw new BleException(BleErrorCode.ConnectionFailed, $"Failed to get services: {servicesResult.Status}");

            _services = servicesResult.Services
                .Select(s => (IBleService)new WindowsBleService(s))
                .ToList();

            _state = BleDeviceState.Connected;
            _stateSubject.OnNext(_state);
        }
        catch (OperationCanceledException)
        {
            _state = BleDeviceState.Failed;
            _stateSubject.OnNext(_state);
            throw new BleException(BleErrorCode.ConnectionTimeout);
        }
        catch (BleException)
        {
            _state = BleDeviceState.Failed;
            _stateSubject.OnNext(_state);
            throw;
        }
        catch (Exception ex)
        {
            _state = BleDeviceState.Failed;
            _stateSubject.OnNext(_state);
            throw new BleException(BleErrorCode.ConnectionFailed, ex.Message, ex);
        }
    }

    private void OnConnectionStatusChanged(BluetoothLEDevice sender, object args)
    {
        var newState = sender.ConnectionStatus == BluetoothConnectionStatus.Connected
            ? BleDeviceState.Connected
            : BleDeviceState.Disconnected;
        _state = newState;
        _stateSubject.OnNext(newState);
    }

    public Task<IReadOnlyList<IBleService>> GetServicesAsync(CancellationToken cancellationToken = default)
    {
        if (_nativeDevice == null) throw new BleException(BleErrorCode.NotConnected);
        if (_services == null) return Task.FromResult<IReadOnlyList<IBleService>>(Array.Empty<IBleService>());
        return Task.FromResult<IReadOnlyList<IBleService>>(_services.AsReadOnly());
    }

    public async Task<IBleService?> GetServiceAsync(Guid serviceUuid, CancellationToken cancellationToken = default)
    {
        var services = await GetServicesAsync(cancellationToken);
        return services.FirstOrDefault(s => s.Uuid == serviceUuid);
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        _state = BleDeviceState.Disconnecting;
        _stateSubject.OnNext(_state);

        // Dispose GattDeviceService COM objects BEFORE disposing BluetoothLEDevice.
        // Windows will not fully release the device — and will therefore continue
        // suppressing its BLE advertisements — until every GattDeviceService
        // reference obtained from GetGattServicesAsync() is closed.  Calling
        // BluetoothLEDevice.Dispose() alone is not sufficient.
        DisposeServices();

        if (_nativeDevice != null)
        {
            // Unsubscribe before disposing so the WinRT COM object is fully
            // released.  Without this the delegate keeps a COM reference alive
            // and Windows continues to treat the device as connected, which
            // suppresses its advertisement events in subsequent scans.
            _nativeDevice.ConnectionStatusChanged -= OnConnectionStatusChanged;
            _nativeDevice.Dispose();
            _nativeDevice = null;
        }
        _state = BleDeviceState.Disconnected;
        _stateSubject.OnNext(_state);
        return Task.CompletedTask;
    }

    private void DisposeServices()
    {
        if (_services == null) return;
        foreach (var svc in _services)
            (svc as IDisposable)?.Dispose();
        _services = null;
    }

    public Task<int> RequestMtuAsync(int mtu, CancellationToken cancellationToken = default)
    {
        // Windows handles MTU negotiation automatically
        return Task.FromResult(_mtu);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        DisposeServices();
        if (_nativeDevice != null)
        {
            _nativeDevice.ConnectionStatusChanged -= OnConnectionStatusChanged;
            _nativeDevice.Dispose();
            _nativeDevice = null;
        }
        _gattSession?.Dispose();
        _stateSubject.OnCompleted();
        _stateSubject.Dispose();
    }
}
