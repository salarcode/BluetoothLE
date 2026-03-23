using global::Linux.Bluetooth;
using Salar.BluetoothLE.Core.Enums;
using Salar.BluetoothLE.Core.Interfaces;
using Salar.BluetoothLE.Core.Models;

namespace Salar.BluetoothLE.Linux;

/// <summary>
/// Implements a BLE characteristic wrapper for Linux GATT operations.
/// </summary>
public sealed class LinuxBleCharacteristic : IBleCharacteristic, IDisposable
{
    private readonly GattCharacteristic _characteristic;
    private readonly string[] _flags;
    private readonly object _handlerLock = new();

    private Action<byte[]>? _notificationHandler;
    private bool _subscribed;
    private bool _disposed;

    /// <summary>
    /// Initializes a new LinuxBleCharacteristic instance.
    /// </summary>
    public LinuxBleCharacteristic(GattCharacteristic characteristic, Guid uuid, string[] flags)
    {
        _characteristic = characteristic;
        Uuid = uuid;
        _flags = flags;
    }

    public Guid Uuid { get; }
    public bool CanRead => HasFlag("read");
    public bool CanWrite => HasFlag("write");
    public bool CanWriteWithoutResponse => HasFlag("write-without-response");
    public bool CanNotify => HasFlag("notify");
    public bool CanIndicate => HasFlag("indicate");

    /// <summary>
    /// Reads the current value of the characteristic.
    /// </summary>
    public async Task<byte[]> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (!CanRead)
            throw new BleException(BleErrorCode.OperationFailed, "Characteristic does not support read.");

        return await _characteristic.ReadValueAsync(new Dictionary<string, object>()).WaitAsync(cancellationToken);
    }

    /// <summary>
    /// Writes data to the characteristic using the specified write mode.
    /// </summary>
    public async Task WriteAsync(byte[] data, WriteType writeType = WriteType.WithResponse, CancellationToken cancellationToken = default)
    {
        var options = new Dictionary<string, object>
        {
            ["type"] = writeType == WriteType.WithoutResponse ? "command" : "request"
        };

        await _characteristic.WriteValueAsync(data, options).WaitAsync(cancellationToken);
    }

    /// <summary>
    /// Starts notifications for the characteristic and registers a handler.
    /// </summary>
    public async Task StartNotificationsAsync(Action<byte[]> handler, CancellationToken cancellationToken = default)
    {
        if (!CanNotify && !CanIndicate)
            throw new BleException(BleErrorCode.OperationFailed, "Characteristic does not support notifications.");

        lock (_handlerLock)
            _notificationHandler = handler;

        if (!_subscribed)
        {
            _characteristic.Value += OnValueAsync;
            _subscribed = true;
        }

        await _characteristic.StartNotifyAsync().WaitAsync(cancellationToken);
    }

    /// <summary>
    /// Stops notifications for the characteristic.
    /// </summary>
    public async Task StopNotificationsAsync(CancellationToken cancellationToken = default)
    {
        lock (_handlerLock)
            _notificationHandler = null;

        await _characteristic.StopNotifyAsync().WaitAsync(cancellationToken);

        if (_subscribed)
        {
            _characteristic.Value -= OnValueAsync;
            _subscribed = false;
        }
    }

    private Task OnValueAsync(GattCharacteristic sender, GattCharacteristicValueEventArgs eventArgs)
    {
        Action<byte[]>? handler;
        lock (_handlerLock)
            handler = _notificationHandler;

        handler?.Invoke(eventArgs.Value);
        return Task.CompletedTask;
    }

    private bool HasFlag(string flag)
        => _flags.Contains(flag, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Releases the underlying Linux Bluetooth characteristic resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_subscribed)
            _characteristic.Value -= OnValueAsync;

        _characteristic.Dispose();
    }
}
