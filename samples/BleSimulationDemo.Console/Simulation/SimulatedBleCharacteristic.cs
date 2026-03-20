using Salar.BluetoothLE.Core.Enums;
using Salar.BluetoothLE.Core.Interfaces;

namespace BleDemo.Console.Simulation;

internal sealed class SimulatedBleCharacteristic : IBleCharacteristic
{
    private readonly string _name;
    private readonly Random _random = new();
    private Action<byte[]>? _notifyHandler;
    private CancellationTokenSource? _notifyCts;

    public Guid Uuid { get; }
    public bool CanRead { get; }
    public bool CanWrite { get; }
    public bool CanWriteWithoutResponse { get; }
    public bool CanNotify { get; }
    public bool CanIndicate { get; }

    public SimulatedBleCharacteristic(
        Guid uuid,
        string name,
        bool canRead = false,
        bool canWrite = false,
        bool canWriteWithoutResponse = false,
        bool canNotify = false,
        bool canIndicate = false)
    {
        Uuid                  = uuid;
        _name                 = name;
        CanRead               = canRead;
        CanWrite              = canWrite;
        CanWriteWithoutResponse = canWriteWithoutResponse;
        CanNotify             = canNotify;
        CanIndicate           = canIndicate;
    }

    public Task<byte[]> ReadAsync(CancellationToken cancellationToken = default)
    {
        // Return 4 random bytes as simulated sensor data
        var data = new byte[4];
        _random.NextBytes(data);
        return Task.FromResult(data);
    }

    public Task WriteAsync(byte[] data, WriteType writeType = WriteType.WithResponse, CancellationToken cancellationToken = default)
        => Task.Delay(50, cancellationToken);

    public Task StartNotificationsAsync(Action<byte[]> handler, CancellationToken cancellationToken = default)
    {
        _notifyHandler = handler;
        _notifyCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = _notifyCts.Token;

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), token);
                if (!token.IsCancellationRequested)
                {
                    var data = new byte[2];
                    _random.NextBytes(data);
                    _notifyHandler?.Invoke(data);
                }
            }
        }, CancellationToken.None);

        return Task.CompletedTask;
    }

    public Task StopNotificationsAsync(CancellationToken cancellationToken = default)
    {
        _notifyCts?.Cancel();
        _notifyCts?.Dispose();
        _notifyCts = null;
        _notifyHandler = null;
        return Task.CompletedTask;
    }
}
