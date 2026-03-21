namespace Salar.BluetoothLE.Core.Models;

/// <summary>
/// Represents a BLE advertisement discovered during scanning.
/// </summary>
public sealed class ScanResult
{
    public string? Name { get; init; }
    public string Address { get; init; } = string.Empty;
    public int Rssi { get; init; }
    public byte[]? AdvertisementData { get; init; }
    public List<Guid> ServiceUuids { get; init; } = new();
    public Dictionary<ushort, byte[]> ManufacturerData { get; init; } = new();
    public bool IsConnectable { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Returns a string representation of the scan result.
    /// </summary>
    public override string ToString() =>
        $"ScanResult {{ Name={Name ?? "(unknown)"}, Address={Address}, Rssi={Rssi}, Connectable={IsConnectable} }}";
}
