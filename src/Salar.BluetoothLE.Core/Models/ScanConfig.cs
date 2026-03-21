namespace Salar.BluetoothLE.Core.Models;

public enum ScanMode
{
    LowPower,
    Balanced,
    LowLatency,
    Opportunistic
}

public sealed class ScanConfig
{
    public static readonly ScanConfig Default = new();

    public TimeSpan Duration { get; init; } = TimeSpan.FromSeconds(10);
    public List<Guid> ServiceUuidFilters { get; init; } = new();
    public ScanMode ScanMode { get; init; } = ScanMode.Balanced;
    public bool AllowDuplicates { get; init; } = false;
    public bool AndroidLegacyScan { get; init; } = false;
}
