using Salar.BluetoothLE.Core.Enums;

namespace Salar.BluetoothLE.Core.Interfaces;

/// <summary>
/// Represents a BLE characteristic that can be read, written, or subscribed to.
/// </summary>
public interface IBleCharacteristic
{
    Guid Uuid { get; }
    bool CanRead { get; }
    bool CanWrite { get; }
    bool CanWriteWithoutResponse { get; }
    bool CanNotify { get; }
    bool CanIndicate { get; }

    /// <summary>
    /// Reads the current value of the characteristic.
    /// </summary>
    Task<byte[]> ReadAsync(CancellationToken cancellationToken = default);
    /// <summary>
    /// Writes data to the characteristic using the specified write mode.
    /// </summary>
    Task WriteAsync(byte[] data, WriteType writeType = WriteType.WithResponse, CancellationToken cancellationToken = default);
    /// <summary>
    /// Starts notifications for the characteristic and registers a handler.
    /// </summary>
    Task StartNotificationsAsync(Action<byte[]> handler, CancellationToken cancellationToken = default);
    /// <summary>
    /// Stops notifications for the characteristic.
    /// </summary>
    Task StopNotificationsAsync(CancellationToken cancellationToken = default);
}
