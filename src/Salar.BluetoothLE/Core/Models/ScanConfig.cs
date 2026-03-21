namespace Salar.BluetoothLE.Core.Models;

/// <summary>
/// Defines the scan performance and power behavior used during BLE discovery.
/// </summary>
public enum ScanMode
{
    LowPower,
    Balanced,
    LowLatency,
    Opportunistic
}

/// <summary>
/// Defines options for controlling BLE scan duration, filters, and platform behavior.
/// </summary>
public sealed class ScanConfig
{
    public static readonly ScanConfig Default = new();

    public TimeSpan Duration { get; init; } = TimeSpan.FromSeconds(10);
    public List<Guid> ServiceUuidFilters { get; init; } = new();
    public ScanMode ScanMode { get; init; } = ScanMode.Balanced;
    public bool AllowDuplicates { get; init; } = false;
    public bool AndroidLegacyScan { get; init; } = false;
}
