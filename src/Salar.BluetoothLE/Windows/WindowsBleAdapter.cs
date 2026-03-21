using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Storage.Streams;
using Salar.BluetoothLE.Core.Abstractions;
using Salar.BluetoothLE.Core.Enums;
using Salar.BluetoothLE.Core.Interfaces;
using Salar.BluetoothLE.Core.Models;

namespace Salar.BluetoothLE.Windows;

public class WindowsBleAdapter : BleAdapterBase
{
    private BluetoothLEAdvertisementWatcher? _watcher;
    private CancellationTokenSource? _scanCts;
    private readonly Dictionary<ulong, WindowsBleDevice> _deviceCache = new();
    private readonly object _cacheLock = new();

    public WindowsBleAdapter()
    {
        AdapterState = BleAdapterState.PoweredOn;
    }

    public override Task<BlePermissionStatus> RequestAccessAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(BlePermissionStatus.Granted);
    }

    public override Task StartScanAsync(ScanConfig? config = null, CancellationToken cancellationToken = default)
    {
        config ??= ScanConfig.Default;

        if (LibraryState == BleLibraryState.Scanning)
            StopScanAsync(cancellationToken).GetAwaiter().GetResult();

        _watcher = new BluetoothLEAdvertisementWatcher();
        _watcher.ScanningMode = BluetoothLEScanningMode.Active;

        foreach (var uuid in config.ServiceUuidFilters)
        {
            var filter = new BluetoothLEAdvertisementFilter();
            filter.Advertisement.ServiceUuids.Add(uuid);
            _watcher.AdvertisementFilter = filter;
        }

        _watcher.Received += OnAdvertisementReceived;
        _watcher.Stopped += OnWatcherStopped;

        _scanCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        _watcher.Start();
        LibraryState = BleLibraryState.Scanning;

        _ = Task.Delay(config.Duration, _scanCts.Token)
            .ContinueWith(async _ => await StopScanAsync(CancellationToken.None), TaskContinuationOptions.NotOnCanceled);

        return Task.CompletedTask;
    }

    private void OnAdvertisementReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
    {
        var serviceUuids = args.Advertisement.ServiceUuids.ToList();
        var manufacturerData = new Dictionary<ushort, byte[]>();
        foreach (var section in args.Advertisement.ManufacturerData)
        {
            var reader = DataReader.FromBuffer(section.Data);
            var bytes = new byte[reader.UnconsumedBufferLength];
            reader.ReadBytes(bytes);
            manufacturerData[section.CompanyId] = bytes;
        }

        var result = new ScanResult
        {
            Name = args.Advertisement.LocalName,
            Address = args.BluetoothAddress.ToString("X"),
            Rssi = args.RawSignalStrengthInDBm,
            IsConnectable = args.IsConnectable,
            ServiceUuids = serviceUuids,
            ManufacturerData = manufacturerData
        };

        PublishScanResult(result);
    }

    private void OnWatcherStopped(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementWatcherStoppedEventArgs args)
    {
        LibraryState = BleLibraryState.Idle;
    }

    public override Task StopScanAsync(CancellationToken cancellationToken = default)
    {
        if (_watcher != null)
        {
            _watcher.Received -= OnAdvertisementReceived;
            _watcher.Stopped -= OnWatcherStopped;
            if (_watcher.Status == BluetoothLEAdvertisementWatcherStatus.Started)
                _watcher.Stop();
            _watcher = null;
        }
        _scanCts?.Cancel();
        _scanCts?.Dispose();
        _scanCts = null;
        LibraryState = BleLibraryState.Idle;
        return Task.CompletedTask;
    }

    public override async Task<IBleDevice> ConnectAsync(string address, ConnectionConfig? config = null, CancellationToken cancellationToken = default)
    {
        config ??= ConnectionConfig.Default;
        ulong bluetoothAddress = Convert.ToUInt64(address, 16);

        WindowsBleDevice device;
        lock (_cacheLock)
        {
            if (!_deviceCache.TryGetValue(bluetoothAddress, out device!))
            {
                device = new WindowsBleDevice(bluetoothAddress);
                _deviceCache[bluetoothAddress] = device;
            }
        }

        LibraryState = BleLibraryState.Connecting;
        try
        {
            await device.ConnectInternalAsync(config, cancellationToken);
            AddConnectedDevice(device);

            // Auto-remove from the connected list and device cache whenever the
            // device disconnects — whether the disconnect was requested or
            // unexpected (e.g. device moved out of range).  This also releases
            // the WinRT BluetoothLEDevice COM reference so that the Windows BLE
            // advertisement watcher can see the device again on the next scan.
            // The subscription disposes itself once it fires so there is no
            // permanent reference keeping the device alive (fixes memory leak
            // on repeated connects).
            IDisposable? stateChangedSub = null;
            stateChangedSub = device.StateChanged.Subscribe(state =>
            {
                if (state is BleDeviceState.Disconnected or BleDeviceState.Failed)
                {
                    RemoveConnectedDevice(device);
                    lock (_cacheLock)
                        _deviceCache.Remove(bluetoothAddress);
                    stateChangedSub?.Dispose();
                    stateChangedSub = null;
                }
            });
        }
        finally
        {
            LibraryState = BleLibraryState.Idle;
        }

        return device;
    }

    public override Task<IBleDevice> ReconnectAsync(string address, ConnectionConfig? config = null, CancellationToken cancellationToken = default)
    {
        ulong bluetoothAddress = Convert.ToUInt64(address, 16);
        RemoveConnectedDevice(address);
        lock (_cacheLock)
        {
            if (_deviceCache.TryGetValue(bluetoothAddress, out var existing))
            {
                existing.Dispose();
                _deviceCache.Remove(bluetoothAddress);
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
                foreach (var device in _deviceCache.Values)
                    device.Dispose();
                _deviceCache.Clear();
            }
        }
        base.Dispose(disposing);
    }
}
