namespace Salar.BluetoothLE.Core.Models;

/// <summary>
/// Defines options for establishing and configuring a BLE device connection.
/// </summary>
public sealed class ConnectionConfig
{
    public static readonly ConnectionConfig Default = new();

    public bool AutoConnect { get; init; } = false;
    public int? RequestMtu { get; init; } = null;
    public TimeSpan ConnectionTimeout { get; init; } = TimeSpan.FromSeconds(30);
}
