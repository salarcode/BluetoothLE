using System.Reactive.Subjects;
using Salar.BluetoothLE.Core.Abstractions;
using Salar.BluetoothLE.Core.Enums;
using Salar.BluetoothLE.Core.Interfaces;
using Salar.BluetoothLE.Core.Models;

namespace BleDemo.Console.Simulation;

/// <summary>
/// A simulated BLE adapter that produces fake scan results and device connections
/// so the demo can run on any platform without real BLE hardware.
/// In a real app, swap this for AndroidBleAdapter / WindowsBleAdapter / IosBleAdapter.
/// </summary>
internal sealed class SimulatedBleAdapter : BleAdapterBase
{
    private static readonly SimulatedDevice[] _knownDevices =
    [
        new SimulatedDevice("AA:BB:CC:DD:EE:01", "Heart Rate Monitor",  -55, [Guid.Parse("0000180D-0000-1000-8000-00805F9B34FB")]),
        new SimulatedDevice("AA:BB:CC:DD:EE:02", "Smart Thermometer",   -70, [Guid.Parse("00001809-0000-1000-8000-00805F9B34FB")]),
        new SimulatedDevice("AA:BB:CC:DD:EE:03", "Fitness Tracker",     -62, [Guid.Parse("00001816-0000-1000-8000-00805F9B34FB")]),
        new SimulatedDevice("AA:BB:CC:DD:EE:04", "BLE Beacon",          -80, []),
        new SimulatedDevice("AA:BB:CC:DD:EE:05", "Glucose Meter",       -68, [Guid.Parse("00001808-0000-1000-8000-00805F9B34FB")]),
    ];

    // Holds last-seen RSSI so "check signal" can return a live-ish value.
    private readonly Dictionary<string, int> _rssiCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Random _random = new();
    private CancellationTokenSource? _scanCts;

    public SimulatedBleAdapter()
    {
        AdapterState = BleAdapterState.PoweredOn;
    }

    public override Task<BlePermissionStatus> RequestAccessAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(BlePermissionStatus.Granted);

    public override async Task StartScanAsync(ScanConfig? config = null, CancellationToken cancellationToken = default)
    {
        config ??= ScanConfig.Default;

        if (LibraryState == BleLibraryState.Scanning)
            await StopScanAsync(cancellationToken);

        _scanCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = _scanCts.Token;

        LibraryState = BleLibraryState.Scanning;

        // Emit scan results gradually (simulates real BLE discovery timing)
        _ = Task.Run(async () =>
        {
            try
            {
                var devices = _knownDevices
                    .Where(d => config.ServiceUuidFilters.Count == 0 ||
                                d.ServiceUuids.Any(u => config.ServiceUuidFilters.Contains(u)))
                    .OrderBy(_ => _random.Next())
                    .ToArray();

                foreach (var dev in devices)
                {
                    if (token.IsCancellationRequested) break;

                    int rssi = dev.BaseRssi + _random.Next(-5, 6);
                    _rssiCache[dev.Address] = rssi;

                    PublishScanResult(new ScanResult
                    {
                        Name        = dev.Name,
                        Address     = dev.Address,
                        Rssi        = rssi,
                        IsConnectable = true,
                        ServiceUuids = dev.ServiceUuids.ToList(),
                        Timestamp   = DateTimeOffset.UtcNow,
                    });

                    await Task.Delay(TimeSpan.FromMilliseconds(400 + _random.Next(0, 600)), token);
                }

                // Auto-stop after duration
                await Task.Delay(config.Duration - TimeSpan.FromSeconds(3), token);
                await StopScanAsync(CancellationToken.None);
            }
            catch (OperationCanceledException) { }
        }, CancellationToken.None);
    }

    public override Task StopScanAsync(CancellationToken cancellationToken = default)
    {
        _scanCts?.Cancel();
        _scanCts?.Dispose();
        _scanCts = null;
        if (LibraryState == BleLibraryState.Scanning)
            LibraryState = BleLibraryState.Idle;
        return Task.CompletedTask;
    }

    public override async Task<IBleDevice> ConnectAsync(
        string address, ConnectionConfig? config = null, CancellationToken cancellationToken = default)
    {
        config ??= ConnectionConfig.Default;
        LibraryState = BleLibraryState.Connecting;

        // Simulate connection delay
        await Task.Delay(TimeSpan.FromSeconds(1.5), cancellationToken);

        var known = _knownDevices.FirstOrDefault(d =>
            string.Equals(d.Address, address, StringComparison.OrdinalIgnoreCase));

        if (known is null)
            throw new InvalidOperationException($"No simulated device found for address {address}.");

        int currentRssi = _rssiCache.TryGetValue(address, out var cached) ? cached : known.BaseRssi;
        var device = new SimulatedBleDevice(known.Address, known.Name, currentRssi, _random);

        AddConnectedDevice(device);
        LibraryState = BleLibraryState.Idle;
        return device;
    }

    public override Task<IBleDevice> ReconnectAsync(
        string address, ConnectionConfig? config = null, CancellationToken cancellationToken = default)
    {
        RemoveConnectedDevice(address);
        return ConnectAsync(address, config, cancellationToken);
    }

    // -------------------------------------------------------------------------
    internal sealed record SimulatedDevice(
        string Address,
        string Name,
        int BaseRssi,
        IReadOnlyList<Guid> ServiceUuids);
}
