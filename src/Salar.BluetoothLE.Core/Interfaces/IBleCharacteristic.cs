using Salar.BluetoothLE.Core.Enums;

namespace Salar.BluetoothLE.Core.Interfaces;

public interface IBleCharacteristic
{
    Guid Uuid { get; }
    bool CanRead { get; }
    bool CanWrite { get; }
    bool CanWriteWithoutResponse { get; }
    bool CanNotify { get; }
    bool CanIndicate { get; }

    Task<byte[]> ReadAsync(CancellationToken cancellationToken = default);
    Task WriteAsync(byte[] data, WriteType writeType = WriteType.WithResponse, CancellationToken cancellationToken = default);
    Task StartNotificationsAsync(Action<byte[]> handler, CancellationToken cancellationToken = default);
    Task StopNotificationsAsync(CancellationToken cancellationToken = default);
}
