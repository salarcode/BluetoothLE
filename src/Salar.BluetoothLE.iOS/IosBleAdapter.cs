using CoreBluetooth;
using Foundation;
using Salar.BluetoothLE.Core.Abstractions;
using Salar.BluetoothLE.Core.Enums;
using Salar.BluetoothLE.Core.Interfaces;
using Salar.BluetoothLE.Core.Models;

namespace Salar.BluetoothLE.iOS;

public class IosBleAdapter : BleAdapterBase
{
    private CBCentralManager? _centralManager;
    private IosCentralManagerDelegate? _centralManagerDelegate;
    private CancellationTokenSource? _scanCts;
    private readonly Dictionary<string, IosBleDevice> _peripheralCache = new();
    private readonly object _cacheLock = new();
    private readonly Dictionary<string, TaskCompletionSource<bool>> _connectionTcs = new();

    public IosBleAdapter()
    {
        InitializeCentralManager();
    }

    private void InitializeCentralManager()
    {
        _centralManagerDelegate = new IosCentralManagerDelegate();
        _centralManagerDelegate.StateUpdated += OnStateUpdated;
        _centralManagerDelegate.OnDiscoveredPeripheral += OnDiscoveredPeripheral;
        _centralManagerDelegate.OnConnectedPeripheral += OnConnectedPeripheral;
        _centralManagerDelegate.OnFailedToConnectPeripheral += OnFailedToConnect;
        _centralManagerDelegate.OnDisconnectedPeripheral += OnDisconnectedPeripheral;
        _centralManager = new CBCentralManager(_centralManagerDelegate, null);
    }

    private void OnStateUpdated(CBCentralManager central)
    {
        AdapterState = central.State switch
        {
            CBManagerState.PoweredOn => BleAdapterState.PoweredOn,
            CBManagerState.PoweredOff => BleAdapterState.PoweredOff,
            CBManagerState.Unauthorized => BleAdapterState.Unauthorized,
            CBManagerState.Unsupported => BleAdapterState.Unavailable,
            CBManagerState.Resetting => BleAdapterState.Resetting,
            _ => BleAdapterState.Unknown
        };
    }

    private void OnDiscoveredPeripheral(CBCentralManager central, CBPeripheral peripheral, NSDictionary advertisementData, NSNumber rssi)
    {
        var serviceUuids = new List<Guid>();
        if (advertisementData.TryGetValue(CBAdvertisement.DataServiceUUIDsKey, out var uuidsObj) && uuidsObj is NSArray uuids)
        {
            for (nuint i = 0; i < uuids.Count; i++)
            {
                if (uuids.GetItem<CBUUID>(i) is { } uuid)
                    serviceUuids.Add(Guid.Parse(uuid.ToString()));
            }
        }

        var manufacturerData = new Dictionary<ushort, byte[]>();
        if (advertisementData.TryGetValue(CBAdvertisement.DataManufacturerDataKey, out var mfDataObj) && mfDataObj is NSData mfData && mfData.Length >= 2)
        {
            var bytes = mfData.ToArray();
            var companyId = (ushort)(bytes[0] | (bytes[1] << 8));
            manufacturerData[companyId] = bytes.Skip(2).ToArray();
        }

        var result = new ScanResult
        {
            Name = peripheral.Name,
            Address = peripheral.Identifier.ToString(),
            Rssi = rssi.Int32Value,
            ServiceUuids = serviceUuids,
            ManufacturerData = manufacturerData,
            IsConnectable = true
        };

        lock (_cacheLock)
        {
            if (!_peripheralCache.ContainsKey(peripheral.Identifier.ToString()))
                _peripheralCache[peripheral.Identifier.ToString()] = new IosBleDevice(peripheral, _centralManager!);
        }

        PublishScanResult(result);
    }

    private void OnConnectedPeripheral(CBCentralManager central, CBPeripheral peripheral)
    {
        var id = peripheral.Identifier.ToString();
        if (_peripheralCache.TryGetValue(id, out var device))
            device.SetConnected();
        if (_connectionTcs.TryGetValue(id, out var tcs))
            tcs.TrySetResult(true);
    }

    private void OnFailedToConnect(CBCentralManager central, CBPeripheral peripheral, NSError? error)
    {
        var id = peripheral.Identifier.ToString();
        if (_peripheralCache.TryGetValue(id, out var device))
            device.SetDisconnected();
        if (_connectionTcs.TryGetValue(id, out var tcs))
            tcs.TrySetException(new BleException(BleErrorCode.ConnectionFailed, error?.LocalizedDescription ?? "Connection failed."));
    }

    private void OnDisconnectedPeripheral(CBCentralManager central, CBPeripheral peripheral, NSError? error)
    {
        var id = peripheral.Identifier.ToString();
        IosBleDevice? device = null;
        lock (_cacheLock)
        {
            if (_peripheralCache.TryGetValue(id, out device))
            {
                // Remove the stale IosBleDevice from the cache so that the next
                // scan or ConnectAsync creates a clean instance with no stale
                // service-discovery cache.  Core Bluetooth retains the
                // CBPeripheral object, so RetrievePeripheralsWithIdentifiers can
                // still find the peripheral for a direct reconnect.
                _peripheralCache.Remove(id);
            }
        }
        if (device != null)
        {
            device.SetDisconnected();
            RemoveConnectedDevice(id);
        }
    }

    public override Task<BlePermissionStatus> RequestAccessAsync(CancellationToken cancellationToken = default)
    {
        var status = CBCentralManager.Authorization switch
        {
            CBManagerAuthorization.AllowedAlways => BlePermissionStatus.Granted,
            CBManagerAuthorization.Denied => BlePermissionStatus.Denied,
            CBManagerAuthorization.Restricted => BlePermissionStatus.Restricted,
            _ => BlePermissionStatus.Unknown
        };
        return Task.FromResult(status);
    }

    public override Task StartScanAsync(ScanConfig? config = null, CancellationToken cancellationToken = default)
    {
        if (_centralManager == null) throw new BleException(BleErrorCode.NotSupported);
        if (AdapterState != BleAdapterState.PoweredOn) throw new BleException(BleErrorCode.NotPoweredOn);

        config ??= ScanConfig.Default;

        var options = new PeripheralScanningOptions { AllowDuplicatesKey = config.AllowDuplicates };
        CBUUID[]? uuidFilters = config.ServiceUuidFilters.Count > 0
            ? config.ServiceUuidFilters.Select(u => CBUUID.FromString(u.ToString())).ToArray()
            : null;

        _scanCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _centralManager.ScanForPeripherals(uuidFilters, options);
        LibraryState = BleLibraryState.Scanning;

        _ = Task.Delay(config.Duration, _scanCts.Token)
            .ContinueWith(async _ => await StopScanAsync(CancellationToken.None), TaskContinuationOptions.NotOnCanceled);

        return Task.CompletedTask;
    }

    public override Task StopScanAsync(CancellationToken cancellationToken = default)
    {
        _centralManager?.StopScan();
        _scanCts?.Cancel();
        _scanCts?.Dispose();
        _scanCts = null;
        LibraryState = BleLibraryState.Idle;
        return Task.CompletedTask;
    }

    public override async Task<IBleDevice> ConnectAsync(string address, ConnectionConfig? config = null, CancellationToken cancellationToken = default)
    {
        if (_centralManager == null) throw new BleException(BleErrorCode.NotSupported);
        if (AdapterState != BleAdapterState.PoweredOn) throw new BleException(BleErrorCode.NotPoweredOn);

        config ??= ConnectionConfig.Default;

        IosBleDevice device;
        lock (_cacheLock)
        {
            if (!_peripheralCache.TryGetValue(address, out device!))
            {
                var peripheralUuid = new NSUuid(address);
                var knownPeripherals = _centralManager.RetrievePeripheralsWithIdentifiers(peripheralUuid);
                if (knownPeripherals.Length == 0)
                    throw new BleException(BleErrorCode.ConnectionFailed, $"No peripheral found with identifier: {address}");
                device = new IosBleDevice(knownPeripherals[0], _centralManager);
                _peripheralCache[address] = device;
            }
        }

        var tcs = new TaskCompletionSource<bool>();
        _connectionTcs[address] = tcs;

        using var reg = cancellationToken.Register(() => tcs.TrySetCanceled());

        LibraryState = BleLibraryState.Connecting;

        var field = typeof(IosBleDevice).GetField("_peripheral", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var peripheral = (CBPeripheral)field!.GetValue(device)!;
        _centralManager.ConnectPeripheral(peripheral);

        var timeoutTask = Task.Delay(config.ConnectionTimeout, cancellationToken);
        var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

        LibraryState = BleLibraryState.Idle;

        if (completedTask == timeoutTask)
            throw new BleException(BleErrorCode.ConnectionTimeout);

        await tcs.Task;
        AddConnectedDevice(device);

        if (config.RequestMtu.HasValue)
            await device.RequestMtuAsync(config.RequestMtu.Value, cancellationToken);

        return device;
    }

    public override Task<IBleDevice> ReconnectAsync(string address, ConnectionConfig? config = null, CancellationToken cancellationToken = default)
    {
        RemoveConnectedDevice(address);
        lock (_cacheLock)
        {
            if (_peripheralCache.TryGetValue(address, out var existing))
            {
                existing.Dispose();
                _peripheralCache.Remove(address);
            }
        }
        return ConnectAsync(address, config, cancellationToken);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            StopScanAsync().GetAwaiter().GetResult();
            lock (_cacheLock)
            {
                foreach (var device in _peripheralCache.Values)
                    device.Dispose();
                _peripheralCache.Clear();
            }
        }
        base.Dispose(disposing);
    }
}
