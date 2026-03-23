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

/// <summary>
/// Implements the BLE adapter for Android scanning and device connections.
/// </summary>
public class AndroidBleAdapter : BleAdapterBase
{
    private readonly BluetoothManager _bluetoothManager;
    private readonly BluetoothAdapter _bluetoothAdapter;
    private readonly Context _context;
    private readonly Context _receiverContext;
    private readonly object _scanLock = new();
    private BluetoothLeScanner? _scanner;
    private AndroidScanCallback? _scanCallback;
    private CancellationTokenSource? _scanCts;
    private BluetoothStateBroadcastReceiver? _bluetoothStateReceiver;
    private long _scanSessionId;
    private readonly Dictionary<string, AndroidBleDevice> _deviceCache = new();
    private readonly object _cacheLock = new();
    private readonly Subject<Exception> _scanErrorSubject = new();

    /// <summary>
    /// Initializes a new AndroidBleAdapter instance.
    /// </summary>
    public AndroidBleAdapter(Context context)
    {
        _context = context;
        _receiverContext = context.ApplicationContext ?? context;
        _bluetoothManager = (BluetoothManager)context.GetSystemService(Context.BluetoothService)!;
        _bluetoothAdapter = _bluetoothManager.Adapter!;
        UpdateAdapterState();
        RegisterBluetoothStateReceiver();
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

    /// <summary>
    /// Requests Bluetooth access for the current platform.
    /// </summary>
    public override Task<BlePermissionStatus> RequestAccessAsync(CancellationToken cancellationToken = default)
    {
        if (_bluetoothAdapter == null)
            return Task.FromResult(BlePermissionStatus.Denied);
        return Task.FromResult(_bluetoothAdapter.IsEnabled ? BlePermissionStatus.Granted : BlePermissionStatus.Denied);
    }

    /// <summary>
    /// Starts scanning for nearby BLE devices.
    /// </summary>
    public override async Task StartScanAsync(ScanConfig? config = null, CancellationToken cancellationToken = default)
    {
        UpdateAdapterState();
        if (AdapterState != BleAdapterState.PoweredOn)
            throw new BleException(BleErrorCode.NotPoweredOn);

        config ??= ScanConfig.Default;

        if (HasActiveScan())
            await StopScanAsync(cancellationToken);

        var scanner = _bluetoothAdapter.BluetoothLeScanner;
        if (scanner == null)
            throw new BleException(BleErrorCode.ScanFailed, "Unable to get BLE scanner.");

        var scanCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var settingsBuilder = new ScanSettings.Builder()!
            .SetScanMode(ToAndroidScanModeLE(config.ScanMode))!;

        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            settingsBuilder.SetLegacy(config.AndroidLegacyScan);

        var settings = settingsBuilder.Build()!;

        var filters = config.ServiceUuidFilters.Count > 0
            ? config.ServiceUuidFilters
                .Select(uuid => new ScanFilter.Builder()!
                    .SetServiceUuid(ParcelUuid.FromString(uuid.ToString()))!
                    .Build()!)
                .ToList()
            : null;

        var scanCallback = new AndroidScanCallback(
            PublishScanResult,
            errorCode => _scanErrorSubject.OnNext(new BleException(BleErrorCode.ScanFailed, $"Scan failed with code: {errorCode}"))
        );

        long scanSessionId;
        lock (_scanLock)
        {
            _scanner = scanner;
            _scanCts = scanCts;
            _scanCallback = scanCallback;
            scanSessionId = ++_scanSessionId;
        }

        LibraryState = BleLibraryState.Scanning;

        scanner.StartScan(filters ?? [], settings, scanCallback);

        _ = StopScanAfterDelayAsync(config.Duration, scanSessionId, scanCts.Token);
    }

    /// <summary>
    /// Stops any active BLE scan.
    /// </summary>
    public override Task StopScanAsync(CancellationToken cancellationToken = default)
    {
        StopScanInternal();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Connects to the BLE device with the specified address.
    /// </summary>
    public override async Task<IBleDevice> ConnectAsync(string address, ConnectionConfig? config = null, CancellationToken cancellationToken = default)
    {
        if (HasActiveScan())
            await StopScanAsync(cancellationToken);

        UpdateAdapterState();
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
                    device.Dispose();
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

    /// <summary>
    /// Reconnects to a previously known BLE device.
    /// </summary>
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

    private bool HasActiveScan()
    {
        lock (_scanLock)
            return _scanner != null || _scanCallback != null || _scanCts != null;
    }

    private async Task StopScanAfterDelayAsync(TimeSpan duration, long scanSessionId, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(duration, cancellationToken).ConfigureAwait(false);
            StopScanInternal(scanSessionId);
        }
        catch (System.OperationCanceledException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error while stopping Bluetooth LE scan: {ex}");
        }
    }

    private void StopScanInternal(long? expectedScanSessionId = null)
    {
        BluetoothLeScanner? scanner;
        AndroidScanCallback? scanCallback;
        CancellationTokenSource? scanCts;

        lock (_scanLock)
        {
            if (expectedScanSessionId.HasValue && expectedScanSessionId.Value != _scanSessionId)
                return;

            scanner = _scanner;
            scanCallback = _scanCallback;
            scanCts = _scanCts;

            if (scanner == null && scanCallback == null && scanCts == null)
                return;

            _scanner = null;
            _scanCallback = null;
            _scanCts = null;
        }

        try
        {
            scanCts?.Cancel();
            if (scanCallback != null)
                scanner?.StopScan(scanCallback);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error while stopping Bluetooth LE scan: {ex}");
            _scanErrorSubject.OnNext(new BleException(BleErrorCode.ScanFailed, "Failed to stop the active BLE scan.", ex));
        }
        finally
        {
            scanCts?.Dispose();
            LibraryState = BleLibraryState.Idle;
        }
    }

    private void RegisterBluetoothStateReceiver()
    {
        _bluetoothStateReceiver = new BluetoothStateBroadcastReceiver(OnBluetoothStateChanged);
        var filter = new IntentFilter(BluetoothAdapter.ActionStateChanged);

        if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
            _receiverContext.RegisterReceiver(_bluetoothStateReceiver, filter, ReceiverFlags.NotExported);
        else
            _receiverContext.RegisterReceiver(_bluetoothStateReceiver, filter);
    }

    private void OnBluetoothStateChanged()
    {
        UpdateAdapterState();

        if (AdapterState != BleAdapterState.PoweredOn)
            StopScanInternal();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            StopScanAsync().GetAwaiter().GetResult();
            if (_bluetoothStateReceiver != null)
            {
                try
                {
                    _receiverContext.UnregisterReceiver(_bluetoothStateReceiver);
                }
                catch (Java.Lang.IllegalArgumentException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Bluetooth state receiver was already unregistered. This can happen if the Android context has already torn it down. {ex}");
                }

                _bluetoothStateReceiver = null;
            }
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

    private sealed class BluetoothStateBroadcastReceiver : BroadcastReceiver
    {
        private readonly Action _onStateChanged;

        public BluetoothStateBroadcastReceiver(Action onStateChanged)
        {
            _onStateChanged = onStateChanged;
        }

        public override void OnReceive(Context? context, Intent? intent)
        {
            if (intent?.Action == BluetoothAdapter.ActionStateChanged)
                _onStateChanged();
        }
    }
}
