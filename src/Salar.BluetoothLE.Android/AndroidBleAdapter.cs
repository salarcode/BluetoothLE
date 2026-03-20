using Android.Bluetooth;
using Android.Bluetooth.LE;
using Android.Content;
using Android.OS;
using Salar.BluetoothLE.Core.Abstractions;
using Salar.BluetoothLE.Core.Enums;
using Salar.BluetoothLE.Core.Interfaces;
using Salar.BluetoothLE.Core.Models;
using System.Reactive.Subjects;
using ScanMode = Android.Bluetooth.LE.ScanMode;

namespace Salar.BluetoothLE.Android;

public class AndroidBleAdapter : BleAdapterBase
{
    private readonly BluetoothManager _bluetoothManager;
    private readonly BluetoothAdapter _bluetoothAdapter;
    private readonly Context _context;
    private BluetoothLeScanner? _scanner;
    private AndroidScanCallback? _scanCallback;
    private CancellationTokenSource? _scanCts;
    private readonly Dictionary<string, AndroidBleDevice> _deviceCache = new();
    private readonly object _cacheLock = new();
    private readonly Subject<Exception> _scanErrorSubject = new();

    public AndroidBleAdapter(Context context)
    {
        _context = context;
        _bluetoothManager = (BluetoothManager)context.GetSystemService(Context.BluetoothService)!;
        _bluetoothAdapter = _bluetoothManager.Adapter!;
        UpdateAdapterState();
    }

    private void UpdateAdapterState()
    {
        if (_bluetoothAdapter == null)
        {
            AdapterState = BleAdapterState.Unavailable;
            return;
        }
        AdapterState = _bluetoothAdapter.IsEnabled ? BleAdapterState.PoweredOn : BleAdapterState.PoweredOff;
    }

    public override Task<BlePermissionStatus> RequestAccessAsync(CancellationToken cancellationToken = default)
    {
        if (_bluetoothAdapter == null)
            return Task.FromResult(BlePermissionStatus.Denied);
        return Task.FromResult(_bluetoothAdapter.IsEnabled ? BlePermissionStatus.Granted : BlePermissionStatus.Denied);
    }

    public override async Task StartScanAsync(ScanConfig? config = null, CancellationToken cancellationToken = default)
    {
        if (AdapterState != BleAdapterState.PoweredOn)
            throw new BleException(BleErrorCode.NotPoweredOn);

        config ??= ScanConfig.Default;

        if (LibraryState == BleLibraryState.Scanning)
            await StopScanAsync(cancellationToken);

        _scanner = _bluetoothAdapter.BluetoothLeScanner;
        if (_scanner == null)
            throw new BleException(BleErrorCode.ScanFailed, "Unable to get BLE scanner.");

        _scanCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var settings = new ScanSettings.Builder()!
            .SetScanMode(ToAndroidScanModeLE(config.ScanMode))!
            .Build()!;

        var filters = config.ServiceUuidFilters.Count > 0
            ? config.ServiceUuidFilters
                .Select(uuid => new ScanFilter.Builder()!
                    .SetServiceUuid(ParcelUuid.FromString(uuid.ToString()))!
                    .Build()!)
                .ToList()
            : null;

        _scanCallback = new AndroidScanCallback(
            result => PublishScanResult(result),
            errorCode => _scanErrorSubject.OnNext(new BleException(BleErrorCode.ScanFailed, $"Scan failed with code: {errorCode}"))
        );

        LibraryState = BleLibraryState.Scanning;

        if (filters != null)
            _scanner.StartScan(filters, settings, _scanCallback);
        else
            _scanner.StartScan(_scanCallback);

        _ = Task.Delay(config.Duration, _scanCts.Token)
            .ContinueWith(async _ => await StopScanAsync(CancellationToken.None), TaskContinuationOptions.NotOnCanceled);
    }

    public override Task StopScanAsync(CancellationToken cancellationToken = default)
    {
        if (LibraryState != BleLibraryState.Scanning) return Task.CompletedTask;

        if (_scanCallback != null)
            _scanner?.StopScan(_scanCallback);

        _scanCts?.Cancel();
        _scanCts?.Dispose();
        _scanCts = null;
        _scanCallback = null;
        LibraryState = BleLibraryState.Idle;
        return Task.CompletedTask;
    }

    public override async Task<IBleDevice> ConnectAsync(string address, ConnectionConfig? config = null, CancellationToken cancellationToken = default)
    {
        if (AdapterState != BleAdapterState.PoweredOn)
            throw new BleException(BleErrorCode.NotPoweredOn);

        config ??= ConnectionConfig.Default;

        AndroidBleDevice device;
        lock (_cacheLock)
        {
            if (!_deviceCache.TryGetValue(address, out device!))
            {
                var nativeDevice = _bluetoothAdapter.GetRemoteDevice(address);
                if (nativeDevice == null)
                    throw new BleException(BleErrorCode.ConnectionFailed, $"Could not find device with address: {address}");

                device = new AndroidBleDevice(nativeDevice, _context);
                _deviceCache[address] = device;
            }
        }

        LibraryState = BleLibraryState.Connecting;
        try
        {
            await device.ConnectInternalAsync(config, cancellationToken);
            AddConnectedDevice(device);

            // Auto-remove from the connected list and device cache whenever the
            // device disconnects — whether the disconnect was requested or
            // unexpected (e.g. device moved out of range).  The subscription
            // disposes itself once it fires so there is no permanent reference
            // keeping the device alive (fixes memory leak on repeated connects).
            IDisposable? stateChangedSub = null;
            stateChangedSub = device.StateChanged.Subscribe(state =>
            {
                if (state is BleDeviceState.Disconnected or BleDeviceState.Failed)
                {
                    RemoveConnectedDevice(device);
                    lock (_cacheLock)
                        _deviceCache.Remove(address);
                    stateChangedSub?.Dispose();
                    stateChangedSub = null;
                }
            });
        }
        catch
        {
            LibraryState = BleLibraryState.Idle;
            throw;
        }
        LibraryState = BleLibraryState.Idle;
        return device;
    }

    public override Task<IBleDevice> ReconnectAsync(string address, ConnectionConfig? config = null, CancellationToken cancellationToken = default)
    {
        RemoveConnectedDevice(address);
        lock (_cacheLock)
        {
            if (_deviceCache.TryGetValue(address, out var existing))
            {
                existing.Dispose();
                _deviceCache.Remove(address);
            }
        }
        return ConnectAsync(address, config, cancellationToken);
    }

    private static ScanMode ToAndroidScanModeLE(Core.Models.ScanMode mode) => mode switch
    {
        Core.Models.ScanMode.LowPower => ScanMode.LowPower,
        Core.Models.ScanMode.Balanced => ScanMode.Balanced,
        Core.Models.ScanMode.LowLatency => ScanMode.LowLatency,
        Core.Models.ScanMode.Opportunistic => ScanMode.Opportunistic,
        _ => ScanMode.Balanced
    };

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            StopScanAsync().GetAwaiter().GetResult();
            lock (_cacheLock)
            {
                foreach (var device in _deviceCache.Values)
                    device.Dispose();
                _deviceCache.Clear();
            }
            _scanErrorSubject.OnCompleted();
            _scanErrorSubject.Dispose();
        }
        base.Dispose(disposing);
    }
}
