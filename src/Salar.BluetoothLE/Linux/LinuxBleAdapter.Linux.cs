using global::Linux.Bluetooth;
using global::Linux.Bluetooth.Extensions;
using Salar.BluetoothLE.Core.Abstractions;
using Salar.BluetoothLE.Core.Enums;
using Salar.BluetoothLE.Core.Interfaces;
using Salar.BluetoothLE.Core.Models;
using Tmds.DBus;

namespace Salar.BluetoothLE.Linux;

/// <summary>
/// Implements the BLE adapter for Linux using BlueZ over D-Bus via Linux.Bluetooth.
/// </summary>
public sealed class LinuxBleAdapter : BleAdapterBase
{
    private readonly object _cacheLock = new();
    private readonly Dictionary<string, LinuxBleDevice> _deviceCache = new(StringComparer.OrdinalIgnoreCase);

    private Adapter? _adapter;
    private CancellationTokenSource? _scanCts;
    private ScanConfig _currentScanConfig = ScanConfig.Default;
    private HashSet<string> _publishedAddresses = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new LinuxBleAdapter instance.
    /// </summary>
    public LinuxBleAdapter()
    {
        AdapterState = BleAdapterState.Unknown;
    }

    /// <summary>
    /// Requests Bluetooth access for the current platform.
    /// </summary>
    public override async Task<BlePermissionStatus> RequestAccessAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var adapter = await EnsureAdapterAsync(cancellationToken).WaitAsync(cancellationToken);
            AdapterState = await adapter.GetPoweredAsync().WaitAsync(cancellationToken)
                ? BleAdapterState.PoweredOn
                : BleAdapterState.PoweredOff;
            return BlePermissionStatus.Granted;
        }
        catch (ConnectException)
        {
            AdapterState = BleAdapterState.Unavailable;
            return BlePermissionStatus.Denied;
        }
        catch (DBusException)
        {
            AdapterState = BleAdapterState.Unavailable;
            return BlePermissionStatus.Denied;
        }
    }

    /// <summary>
    /// Starts scanning for nearby BLE devices.
    /// </summary>
    public override async Task StartScanAsync(ScanConfig? config = null, CancellationToken cancellationToken = default)
    {
        config ??= ScanConfig.Default;

        if (LibraryState == BleLibraryState.Scanning)
            await StopScanAsync(cancellationToken).WaitAsync(cancellationToken);

        var adapter = await EnsureAdapterAsync(cancellationToken).WaitAsync(cancellationToken);
        if (!await adapter.GetPoweredAsync().WaitAsync(cancellationToken))
        {
            AdapterState = BleAdapterState.PoweredOff;
            throw new BleException(BleErrorCode.NotPoweredOn);
        }

        _currentScanConfig = config;
        _publishedAddresses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _scanCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            await adapter.StartDiscoveryAsync().WaitAsync(cancellationToken);
        }
        catch (DBusException ex) when (ex.ErrorName == "org.bluez.Error.InProgress")
        {
            await adapter.StopDiscoveryAsync().WaitAsync(cancellationToken);
            await adapter.StartDiscoveryAsync().WaitAsync(cancellationToken);
        }

        LibraryState = BleLibraryState.Scanning;

        var knownDevices = await adapter.GetDevicesAsync().WaitAsync(cancellationToken);
        foreach (var device in knownDevices)
            await PublishDeviceAsync(device, _scanCts.Token).WaitAsync(_scanCts.Token);

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(config.Duration, _scanCts.Token);
                if (!_scanCts.Token.IsCancellationRequested)
                    await StopScanAsync(CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
            }
        });
    }

    /// <summary>
    /// Stops any active BLE scan.
    /// </summary>
    public override async Task StopScanAsync(CancellationToken cancellationToken = default)
    {
        var adapter = _adapter;
        var scanCts = _scanCts;
        _scanCts = null;

        scanCts?.Cancel();
        scanCts?.Dispose();

        if (adapter != null)
        {
            try
            {
                await adapter.StopDiscoveryAsync().WaitAsync(cancellationToken);
            }
            catch (DBusException ex) when (ex.ErrorName == "org.bluez.Error.Failed" &&
                                           ex.ErrorMessage.Contains("No discovery started", StringComparison.OrdinalIgnoreCase))
            {
            }
        }

        LibraryState = BleLibraryState.Idle;
    }

    /// <summary>
    /// Connects to the BLE device with the specified address.
    /// </summary>
    public override async Task<IBleDevice> ConnectAsync(string address, ConnectionConfig? config = null, CancellationToken cancellationToken = default)
    {
        config ??= ConnectionConfig.Default;
        var adapter = await EnsureAdapterAsync(cancellationToken).WaitAsync(cancellationToken);
        var normalizedAddress = NormalizeAddress(address);

        LibraryState = BleLibraryState.Connecting;
        try
        {
            var nativeDevice = await adapter.GetDeviceAsync(normalizedAddress).WaitAsync(cancellationToken);
            if (nativeDevice == null)
                throw new BleException(BleErrorCode.ConnectionFailed, $"BLE device '{normalizedAddress}' was not found.");

            var device = GetOrCreateDevice(nativeDevice, normalizedAddress);
            await device.ConnectInternalAsync(config, cancellationToken).WaitAsync(cancellationToken);
            AddConnectedDevice(device);
            return device;
        }
        finally
        {
            LibraryState = BleLibraryState.Idle;
        }
    }

    /// <summary>
    /// Reconnects to a previously known BLE device.
    /// </summary>
    public override async Task<IBleDevice> ReconnectAsync(string address, ConnectionConfig? config = null, CancellationToken cancellationToken = default)
    {
        var normalizedAddress = NormalizeAddress(address);

        lock (_cacheLock)
        {
            if (_deviceCache.TryGetValue(normalizedAddress, out var existing))
            {
                _deviceCache.Remove(normalizedAddress);
                RemoveConnectedDevice(existing);
                existing.Dispose();
            }
        }

        return await ConnectAsync(normalizedAddress, config, cancellationToken).WaitAsync(cancellationToken);
    }

    private async Task<Adapter> EnsureAdapterAsync(CancellationToken cancellationToken)
    {
        if (_adapter != null)
            return _adapter;

        var adapters = await BlueZManager.GetAdaptersAsync().WaitAsync(cancellationToken);
        _adapter = adapters.FirstOrDefault()
            ?? throw new BleException(BleErrorCode.NotSupported, "No Linux Bluetooth adapters were found.");

        _adapter.PoweredOn += OnAdapterPoweredOnAsync;
        _adapter.PoweredOff += OnAdapterPoweredOffAsync;
        _adapter.DeviceFound += OnDeviceFoundAsync;

        AdapterState = await _adapter.GetPoweredAsync().WaitAsync(cancellationToken)
            ? BleAdapterState.PoweredOn
            : BleAdapterState.PoweredOff;

        return _adapter;
    }

    private Task OnAdapterPoweredOnAsync(Adapter sender, BlueZEventArgs eventArgs)
    {
        AdapterState = BleAdapterState.PoweredOn;
        return Task.CompletedTask;
    }

    private Task OnAdapterPoweredOffAsync(Adapter sender, BlueZEventArgs eventArgs)
    {
        AdapterState = BleAdapterState.PoweredOff;
        return Task.CompletedTask;
    }

    private Task OnDeviceFoundAsync(Adapter sender, DeviceFoundEventArgs eventArgs)
    {
        if (LibraryState != BleLibraryState.Scanning || _scanCts == null || _scanCts.IsCancellationRequested)
            return Task.CompletedTask;

        return PublishDeviceAsync(eventArgs.Device, _scanCts.Token);
    }

    private async Task PublishDeviceAsync(Device device, CancellationToken cancellationToken)
    {
        DeviceProperties properties;
        try
        {
            properties = await device.GetPropertiesAsync().WaitAsync(cancellationToken);
        }
        catch
        {
            return;
        }

        if (!MatchesScanFilters(properties))
            return;

        var address = NormalizeAddress(properties.Address);
        if (string.IsNullOrWhiteSpace(address))
            return;

        if (!_currentScanConfig.AllowDuplicates && !_publishedAddresses.Add(address))
            return;

        PublishScanResult(new ScanResult
        {
            Name = string.IsNullOrWhiteSpace(properties.Alias) ? properties.Name : properties.Alias,
            Address = address,
            Rssi = properties.Rssi,
            IsConnectable = true,
            ServiceUuids = properties.UUIDs?
                .Select(TryParseUuid)
                .Where(uuid => uuid.HasValue)
                .Select(uuid => uuid!.Value)
                .ToList() ?? new List<Guid>(),
            ManufacturerData = ConvertManufacturerData(properties.ManufacturerData),
        });
    }

    private bool MatchesScanFilters(DeviceProperties properties)
    {
        if (_currentScanConfig.ServiceUuidFilters.Count == 0)
            return true;

        var available = properties.UUIDs?
            .Select(TryParseUuid)
            .Where(uuid => uuid.HasValue)
            .Select(uuid => uuid!.Value)
            .ToHashSet() ?? [];

        return _currentScanConfig.ServiceUuidFilters.Any(available.Contains);
    }

    private LinuxBleDevice GetOrCreateDevice(Device nativeDevice, string normalizedAddress)
    {
        lock (_cacheLock)
        {
            if (_deviceCache.TryGetValue(normalizedAddress, out var existing))
                return existing;

            var device = new LinuxBleDevice(nativeDevice, OnDeviceDisconnected, normalizedAddress);
            _deviceCache[normalizedAddress] = device;
            return device;
        }
    }

    private void OnDeviceDisconnected(LinuxBleDevice device)
    {
        RemoveConnectedDevice(device);

        lock (_cacheLock)
            _deviceCache.Remove(device.Id);
    }

    private static string NormalizeAddress(string? address)
        => string.IsNullOrWhiteSpace(address)
            ? string.Empty
            : address.Replace('-', ':').Trim().ToUpperInvariant();

    private static Guid? TryParseUuid(string? uuid)
    {
        if (string.IsNullOrWhiteSpace(uuid))
            return null;

        return Guid.TryParse(BlueZManager.NormalizeUUID(uuid), out var parsed) ? parsed : null;
    }

    private static Dictionary<ushort, byte[]> ConvertManufacturerData(IDictionary<ushort, object>? manufacturerData)
    {
        if (manufacturerData == null || manufacturerData.Count == 0)
            return new Dictionary<ushort, byte[]>();

        var result = new Dictionary<ushort, byte[]>();
        foreach (var (key, value) in manufacturerData)
        {
            if (TryConvertManufacturerPayload(value, out var bytes))
                result[key] = bytes;
        }

        return result;
    }

    private static bool TryConvertManufacturerPayload(object? value, out byte[] bytes)
    {
        switch (value)
        {
            case byte[] array:
                bytes = array;
                return true;
            case IReadOnlyList<byte> list:
                bytes = list.ToArray();
                return true;
            case IEnumerable<byte> enumerable:
                bytes = enumerable.ToArray();
                return true;
            default:
                bytes = Array.Empty<byte>();
                return false;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            StopScanAsync(CancellationToken.None).GetAwaiter().GetResult();

            if (_adapter != null)
            {
                _adapter.PoweredOn -= OnAdapterPoweredOnAsync;
                _adapter.PoweredOff -= OnAdapterPoweredOffAsync;
                _adapter.DeviceFound -= OnDeviceFoundAsync;
                _adapter.Dispose();
                _adapter = null;
            }

            lock (_cacheLock)
            {
                foreach (var device in _deviceCache.Values)
                    device.Dispose();
                _deviceCache.Clear();
            }
        }

        base.Dispose(disposing);
    }
}
