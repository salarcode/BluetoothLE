using System.Reactive.Subjects;
using Salar.BluetoothLE.Core.Enums;
using Salar.BluetoothLE.Core.Interfaces;
using Salar.BluetoothLE.Core.Models;

namespace Salar.BluetoothLE.Core.Abstractions;

public abstract class BleAdapterBase : IBleAdapter, IDisposable
{
    private readonly Subject<BleAdapterState> _adapterStateSubject = new();
    private readonly Subject<ScanResult> _scanResultSubject = new();
    private readonly Subject<BleLibraryState> _libraryStateSubject = new();
    private readonly List<IBleDevice> _connectedDevices = new();
    private readonly object _lock = new();
    private bool _disposed;

    private BleAdapterState _adapterState = BleAdapterState.Unknown;
    private BleLibraryState _libraryState = BleLibraryState.Idle;

    public BleAdapterState AdapterState
    {
        get => _adapterState;
        protected set
        {
            if (_adapterState == value) return;
            _adapterState = value;
            _adapterStateSubject.OnNext(value);
        }
    }

    public BleLibraryState LibraryState
    {
        get => _libraryState;
        protected set
        {
            if (_libraryState == value) return;
            _libraryState = value;
            _libraryStateSubject.OnNext(value);
        }
    }

    public IReadOnlyList<IBleDevice> ConnectedDevices
    {
        get
        {
            lock (_lock)
                return _connectedDevices.AsReadOnly();
        }
    }

    public IObservable<BleAdapterState> AdapterStateChanged => _adapterStateSubject;
    public IObservable<ScanResult> ScanResultReceived => _scanResultSubject;
    public IObservable<BleLibraryState> LibraryStateChanged => _libraryStateSubject;

    protected void PublishScanResult(ScanResult result) => _scanResultSubject.OnNext(result);

    protected void AddConnectedDevice(IBleDevice device)
    {
        lock (_lock)
        {
            if (!_connectedDevices.Contains(device))
                _connectedDevices.Add(device);
        }
    }

    protected void RemoveConnectedDevice(IBleDevice device)
    {
        lock (_lock)
            _connectedDevices.Remove(device);
    }

    protected void RemoveConnectedDevice(string id)
    {
        lock (_lock)
        {
            var device = _connectedDevices.FirstOrDefault(d => d.Id == id);
            if (device != null)
                _connectedDevices.Remove(device);
        }
    }

    public abstract Task<BlePermissionStatus> RequestAccessAsync(CancellationToken cancellationToken = default);
    public abstract Task StartScanAsync(ScanConfig? config = null, CancellationToken cancellationToken = default);
    public abstract Task StopScanAsync(CancellationToken cancellationToken = default);
    public abstract Task<IBleDevice> ConnectAsync(string address, ConnectionConfig? config = null, CancellationToken cancellationToken = default);
    public abstract Task<IBleDevice> ReconnectAsync(string address, ConnectionConfig? config = null, CancellationToken cancellationToken = default);

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            _adapterStateSubject.OnCompleted();
            _adapterStateSubject.Dispose();
            _scanResultSubject.OnCompleted();
            _scanResultSubject.Dispose();
            _libraryStateSubject.OnCompleted();
            _libraryStateSubject.Dispose();

            lock (_lock)
            {
                foreach (var device in _connectedDevices)
                    device.Dispose();
                _connectedDevices.Clear();
            }
        }
        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
