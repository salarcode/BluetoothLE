using Android.Bluetooth;
using Android.Content;
using Salar.BluetoothLE.Core.Enums;
using Salar.BluetoothLE.Core.Interfaces;
using Salar.BluetoothLE.Core.Models;
using System.Reactive.Subjects;

namespace Salar.BluetoothLE.Android;

/// <summary>
/// Implements a BLE device wrapper over Android GATT operations.
/// </summary>
public class AndroidBleDevice : IBleDevice
{
    private readonly BluetoothDevice _nativeDevice;
    private readonly Context _context;
    private readonly AndroidGattCallback _gattCallback;
    private BluetoothGatt? _gatt;
    private readonly Subject<BleDeviceState> _stateSubject = new();
    private BleDeviceState _state = BleDeviceState.Disconnected;
    private int _mtu = 23;
    private bool _disposed;

    private TaskCompletionSource<bool>? _connectionTcs;
    private TaskCompletionSource<bool>? _disconnectTcs;
    private TaskCompletionSource<bool>? _serviceDiscoveryTcs;
    private TaskCompletionSource<byte[]>? _readTcs;
    private TaskCompletionSource<bool>? _writeTcs;
    private TaskCompletionSource<bool>? _descriptorWriteTcs;
    private TaskCompletionSource<int>? _mtuTcs;
    private static readonly TimeSpan DisconnectSettleDelay = TimeSpan.FromMilliseconds(300);

    private readonly Dictionary<Guid, Action<byte[]>> _notificationHandlers = [];
    private List<IBleService>? _services;

    internal bool IsDisposed => _disposed;

    public string Id => _nativeDevice.Address ?? string.Empty;
    public string? Name => _nativeDevice.Name;
    public BleDeviceState State => _state;
    public int Mtu => _mtu;
    public IObservable<BleDeviceState> StateChanged => _stateSubject;

    /// <summary>
    /// Initializes a new AndroidBleDevice instance.
    /// </summary>
    public AndroidBleDevice(BluetoothDevice device, Context context)
    {
        _nativeDevice = device;
        _context = context;
        _gattCallback = new AndroidGattCallback();
        _gattCallback.ConnectionStateChanged += OnConnectionStateChanged;
        _gattCallback.ServicesDiscovered += OnServicesDiscovered;
        _gattCallback.CharacteristicRead += OnCharacteristicRead;
        _gattCallback.CharacteristicWritten += OnCharacteristicWritten;
        _gattCallback.CharacteristicChanged += OnCharacteristicChanged;
        _gattCallback.DescriptorWritten += OnDescriptorWritten;
        _gattCallback.MtuChanged += OnMtuChanged;
    }

    private void OnConnectionStateChanged(BluetoothGatt gatt, ProfileState newState)
    {
        if (!ShouldHandleGatt(gatt))
            return;

        if (newState == ProfileState.Connected)
        {
            PublishState(BleDeviceState.Connected);
            _connectionTcs?.TrySetResult(true);
        }
        else if (newState == ProfileState.Disconnected)
        {
            if (_disconnectTcs != null)
            {
                // Intentional disconnect via DisconnectAsync — let that method
                // call Close() and update the final state once we signal here.
                _disconnectTcs.TrySetResult(true);
            }
            else
            {
                // Unexpected disconnect (e.g. device moved out of range).
                PublishState(BleDeviceState.Disconnected);
            }
            // Unblock any pending connection attempt.
            _connectionTcs?.TrySetResult(false);
        }
    }

    private void OnServicesDiscovered(BluetoothGatt gatt, GattStatus status)
    {
        if (!ShouldHandleGatt(gatt))
            return;

        _serviceDiscoveryTcs?.TrySetResult(status == GattStatus.Success);
    }

    private void OnCharacteristicRead(BluetoothGatt gatt, BluetoothGattCharacteristic characteristic, GattStatus status)
    {
        if (!ShouldHandleGatt(gatt))
            return;

        if (status == GattStatus.Success)
            _readTcs?.TrySetResult(characteristic.GetValue() ?? Array.Empty<byte>());
        else
            _readTcs?.TrySetException(new BleException(BleErrorCode.OperationFailed, $"Read failed with status: {status}"));
    }

    private void OnCharacteristicWritten(BluetoothGatt gatt, BluetoothGattCharacteristic characteristic, GattStatus status)
    {
        if (!ShouldHandleGatt(gatt))
            return;

        if (status == GattStatus.Success)
            _writeTcs?.TrySetResult(true);
        else
            _writeTcs?.TrySetException(new BleException(BleErrorCode.OperationFailed, $"Write failed with status: {status}"));
    }

    private void OnCharacteristicChanged(BluetoothGatt gatt, BluetoothGattCharacteristic characteristic)
    {
        if (!ShouldHandleGatt(gatt))
            return;

        var uuid = Guid.Parse(characteristic.Uuid!.ToString()!);
        if (_notificationHandlers.TryGetValue(uuid, out var handler))
            handler(characteristic.GetValue() ?? Array.Empty<byte>());
    }

    private void OnDescriptorWritten(BluetoothGatt gatt, GattStatus status)
    {
        if (!ShouldHandleGatt(gatt))
            return;

        if (status == GattStatus.Success)
            _descriptorWriteTcs?.TrySetResult(true);
        else
            _descriptorWriteTcs?.TrySetException(new BleException(BleErrorCode.OperationFailed, $"Descriptor write failed: {status}"));
    }

    private void OnMtuChanged(BluetoothGatt gatt, int mtu, GattStatus status)
    {
        if (!ShouldHandleGatt(gatt))
            return;

        if (status == GattStatus.Success)
        {
            _mtu = mtu;
            _mtuTcs?.TrySetResult(mtu);
        }
        else
        {
            _mtuTcs?.TrySetException(new BleException(BleErrorCode.OperationFailed, "MTU request failed."));
        }
    }

    internal async Task ConnectInternalAsync(ConnectionConfig config, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        if (_gatt != null)
            CloseGatt(_gatt, disconnectFirst: true);

        _gatt = null;
        _services = null;

        PublishState(BleDeviceState.Connecting);

        _connectionTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var reg = cancellationToken.Register(() => _connectionTcs.TrySetCanceled());

        _gatt = _nativeDevice.ConnectGatt(_context, config.AutoConnect, _gattCallback);
        if (_gatt == null)
        {
            _connectionTcs = null;
            throw new BleException(BleErrorCode.ConnectionFailed, "ConnectGatt returned null.");
        }

        var timeoutTask = Task.Delay(config.ConnectionTimeout, cancellationToken);
        var completedTask = await Task.WhenAny(_connectionTcs.Task, timeoutTask);

        if (completedTask == timeoutTask)
        {
            _connectionTcs = null;
            CloseGatt(_gatt, disconnectFirst: true);
            _gatt = null;
            throw new BleException(BleErrorCode.ConnectionTimeout);
        }

        var connected = await _connectionTcs.Task;
        _connectionTcs = null;
        if (!connected)
        {
            CloseGatt(_gatt, disconnectFirst: true);
            _gatt = null;
            throw new BleException(BleErrorCode.ConnectionFailed);
        }

        _serviceDiscoveryTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = _gatt.DiscoverServices();
        await _serviceDiscoveryTcs.Task;
        _serviceDiscoveryTcs = null;

        if (config.RequestMtu.HasValue)
            await RequestMtuAsync(config.RequestMtu.Value, cancellationToken);
    }

    /// <summary>
    /// Gets the GATT services exposed by this device.
    /// </summary>
    public async Task<IReadOnlyList<IBleService>> GetServicesAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (_gatt == null) throw new BleException(BleErrorCode.NotConnected);
        if (_services == null)
        {
            _services = _gatt.Services!
                .Select(s => (IBleService)new AndroidBleService(s, this))
                .ToList();
        }
        return await Task.FromResult<IReadOnlyList<IBleService>>(_services.AsReadOnly());
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
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            return;

        if (_gatt == null)
        {
            if (_state != BleDeviceState.Disconnected)
                PublishState(BleDeviceState.Disconnected);
            return;
        }

        PublishState(BleDeviceState.Disconnecting);

        _disconnectTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var reg = cancellationToken.Register(() => _disconnectTcs.TrySetCanceled());

        _gatt.Disconnect();

        try
        {
            // Wait for Android BT stack to confirm disconnection via
            // onConnectionStateChange(STATE_DISCONNECTED) before calling
            // Close().  Calling Close() before this callback fires leaves a
            // zombie GATT connection in Android's stack, which prevents the
            // device from appearing in subsequent scan results — especially
            // after GATT service discovery has been performed.
            await _disconnectTcs.Task;
        }
        catch (OperationCanceledException)
        {
            // Best-effort on cancellation — proceed with cleanup anyway.
        }
        finally
        {
            _disconnectTcs = null;
            // Some Android stacks keep the device effectively "stuck" until the
            // next adapter reset unless the GATT cache is refreshed and the
            // stack gets a brief moment to settle before Close().
            TryRefreshGatt(_gatt);
            await Task.Delay(DisconnectSettleDelay).ConfigureAwait(false);
            try
            {
                _gatt.Close();
                _gatt.Dispose();
            }
            catch
            {
                // ignored
            }
            _gatt = null;
            _services = null;
        }

        PublishState(BleDeviceState.Disconnected);
    }

    /// <summary>
    /// Requests the specified MTU for the BLE connection.
    /// </summary>
    public async Task<int> RequestMtuAsync(int mtu, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (_gatt == null) throw new BleException(BleErrorCode.NotConnected);
        _mtuTcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var reg = cancellationToken.Register(() => _mtuTcs.TrySetCanceled());
        _gatt.RequestMtu(mtu);
        try
        {
            return await _mtuTcs.Task;
        }
        finally
        {
            _mtuTcs = null;
        }
    }

    internal async Task<byte[]> ReadCharacteristicAsync(BluetoothGattCharacteristic characteristic, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        if (_gatt == null) throw new BleException(BleErrorCode.NotConnected);
        _readTcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var reg = cancellationToken.Register(() => _readTcs.TrySetCanceled());
        _gatt.ReadCharacteristic(characteristic);
        try
        {
            return await _readTcs.Task;
        }
        finally
        {
            _readTcs = null;
        }
    }

    internal async Task WriteCharacteristicAsync(BluetoothGattCharacteristic characteristic, byte[] data, WriteType writeType, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        if (_gatt == null) throw new BleException(BleErrorCode.NotConnected);
        characteristic.SetValue(data);
        characteristic.WriteType = writeType == WriteType.WithoutResponse
            ? GattWriteType.NoResponse
            : GattWriteType.Default;
        _writeTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var reg = cancellationToken.Register(() => _writeTcs.TrySetCanceled());
        _gatt.WriteCharacteristic(characteristic);
        try
        {
            await _writeTcs.Task;
        }
        finally
        {
            _writeTcs = null;
        }
    }

    internal async Task SetCharacteristicNotificationAsync(BluetoothGattCharacteristic characteristic, bool enable, Action<byte[]>? handler, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        if (_gatt == null) throw new BleException(BleErrorCode.NotConnected);
        var uuid = Guid.Parse(characteristic.Uuid!.ToString()!);

        _gatt.SetCharacteristicNotification(characteristic, enable);

        var descriptor = characteristic.GetDescriptor(Java.Util.UUID.FromString("00002902-0000-1000-8000-00805f9b34fb"));
        if (descriptor != null)
        {
            descriptor.SetValue(enable ? BluetoothGattDescriptor.EnableNotificationValue!.ToArray() : BluetoothGattDescriptor.DisableNotificationValue!.ToArray());
            _descriptorWriteTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var reg = cancellationToken.Register(() => _descriptorWriteTcs.TrySetCanceled());
            _gatt.WriteDescriptor(descriptor);
            try
            {
                await _descriptorWriteTcs.Task;
            }
            finally
            {
                _descriptorWriteTcs = null;
            }
        }

        if (enable && handler != null)
            _notificationHandlers[uuid] = handler;
        else
            _notificationHandlers.Remove(uuid);
    }

    /// <summary>
    /// Releases the device and any native Android GATT resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _gattCallback.ConnectionStateChanged -= OnConnectionStateChanged;
        _gattCallback.ServicesDiscovered -= OnServicesDiscovered;
        _gattCallback.CharacteristicRead -= OnCharacteristicRead;
        _gattCallback.CharacteristicWritten -= OnCharacteristicWritten;
        _gattCallback.CharacteristicChanged -= OnCharacteristicChanged;
        _gattCallback.DescriptorWritten -= OnDescriptorWritten;
        _gattCallback.MtuChanged -= OnMtuChanged;

        FailPendingOperations(new ObjectDisposedException(nameof(AndroidBleDevice)));

        var gatt = _gatt;
        _gatt = null;
        _services = null;
        _notificationHandlers.Clear();

        if (gatt != null)
        {
            try
            {
                gatt.Disconnect();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error while disconnecting Bluetooth GATT during dispose: {ex}");
            }

            TryRefreshGatt(gatt);

            try
            {
                gatt.Close();
                gatt.Dispose();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error while closing Bluetooth GATT during dispose: {ex}");
            }
        }

        _stateSubject.OnCompleted();
        _stateSubject.Dispose();
    }

    private bool ShouldHandleGatt(BluetoothGatt gatt)
        => !_disposed && _gatt != null && ReferenceEquals(gatt, _gatt);

    private void PublishState(BleDeviceState state)
    {
        _state = state;

        if (_disposed)
            return;

        try
        {
            _stateSubject.OnNext(state);
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private void FailPendingOperations(Exception ex)
    {
        _connectionTcs?.TrySetException(ex);
        _disconnectTcs?.TrySetException(ex);
        _serviceDiscoveryTcs?.TrySetException(ex);
        _readTcs?.TrySetException(ex);
        _writeTcs?.TrySetException(ex);
        _descriptorWriteTcs?.TrySetException(ex);
        _mtuTcs?.TrySetException(ex);
    }

    private static void CloseGatt(BluetoothGatt? gatt, bool disconnectFirst)
    {
        if (gatt == null)
            return;

        if (disconnectFirst)
        {
            try
            {
                gatt.Disconnect();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error while disconnecting Bluetooth GATT: {ex}");
            }
        }

        TryRefreshGatt(gatt);

        try
        {
            gatt.Close();
            gatt.Dispose();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error while closing Bluetooth GATT: {ex}");
        }
    }

    private static void TryRefreshGatt(BluetoothGatt? gatt)
    {
        if (gatt == null)
            return;

        try
        {
            var refreshMethod = gatt.Class?.GetMethod("refresh");
            refreshMethod?.Invoke(gatt);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error while refreshing Bluetooth GATT cache: {ex}");
        }
    }
}
