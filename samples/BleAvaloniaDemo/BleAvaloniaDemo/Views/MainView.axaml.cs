using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Salar.BluetoothLE;
using Salar.BluetoothLE.Core.Enums;
using Salar.BluetoothLE.Core.Interfaces;
using Salar.BluetoothLE.Core.Models;

namespace BleAvaloniaDemo.Views;

public partial class MainView : UserControl
{
    private readonly ObservableCollection<ScanResultItem> _scanResults = [];
    private readonly ObservableCollection<ConnectedDeviceItem> _connectedDevices = [];
    private readonly ObservableCollection<string> _serviceLines = [];
    private readonly Dictionary<string, ScanResult> _latestScanResults = new(StringComparer.OrdinalIgnoreCase);

    private readonly IBleAdapter? _adapter;
    private readonly Func<Task<bool>> _requestBluetoothAccessAsync;
    private readonly IDisposable? _scanSubscription;
    private readonly IDisposable? _adapterStateSubscription;
    private readonly IDisposable? _libraryStateSubscription;
    private IBleDevice? _selectedDevice;
    private bool _isBusy;

    public MainView()
    {
        InitializeComponent();

        ScanResultsView.ItemsSource = _scanResults;
        ConnectedDevicesView.ItemsSource = _connectedDevices;
        ServicesView.ItemsSource = _serviceLines;

        _requestBluetoothAccessAsync = BleDemoPlatformServices.RequestBluetoothAccessAsync;

        try
        {
            _adapter = BleDemoPlatformServices.CreateAdapter?.Invoke();
        }
        catch (Exception ex)
        {
            FeedbackLabel.Text = ex.Message;
        }

        if (_adapter is not null)
        {
            _scanSubscription = _adapter.ScanResultReceived.Subscribe(result => Dispatcher.UIThread.Post(() => OnScanResultReceived(result)));
            _adapterStateSubscription = _adapter.AdapterStateChanged.Subscribe(_ => Dispatcher.UIThread.Post(UpdateStatus));
            _libraryStateSubscription = _adapter.LibraryStateChanged.Subscribe(state => Dispatcher.UIThread.Post(() => OnLibraryStateChanged(state)));

            RefreshConnectedDevices();
        }
        else if (string.IsNullOrWhiteSpace(FeedbackLabel.Text))
        {
            FeedbackLabel.Text = "No BLE adapter is configured for this platform.";
        }

        UpdateStatus();
        UpdateSelectedDeviceDetails();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _scanSubscription?.Dispose();
        _adapterStateSubscription?.Dispose();
        _libraryStateSubscription?.Dispose();
        base.OnDetachedFromVisualTree(e);
    }

    private void OnScanResultReceived(ScanResult result)
    {
        _latestScanResults[result.Address] = result;
        RebuildScanResults();

        if (_selectedDevice?.Id == result.Address)
        {
            SignalLabel.Text = BuildSignalSummary(result.Rssi);
        }
    }

    private void OnLibraryStateChanged(BleLibraryState state)
    {
        UpdateStatus();

        if (_isBusy)
        {
            return;
        }

        FeedbackLabel.Text = state switch
        {
            BleLibraryState.Scanning => "Scanning for nearby BLE devices...",
            BleLibraryState.Connecting => "Connecting to device...",
            _ when _latestScanResults.Count > 0 => $"Scan complete. Found {_latestScanResults.Count} device(s).",
            _ => "Ready to scan for nearby BLE devices."
        };
    }

    private async void OnRequestAccessClicked(object? sender, RoutedEventArgs e)
    {
        await RequestAccessAsync();
    }

    private async void OnScanClicked(object? sender, RoutedEventArgs e)
    {
        if (!await RequestAccessAsync())
        {
            return;
        }

        await RunBusyAsync("Starting BLE scan...", async () =>
        {
            if (_adapter is null)
            {
                return;
            }

            _latestScanResults.Clear();
            _scanResults.Clear();
            await _adapter.StartScanAsync(new ScanConfig
            {
                Duration = TimeSpan.FromSeconds(10),
                ScanMode = ScanMode.Balanced,
                AllowDuplicates = false,
                AndroidLegacyScan = false,
            });
            FeedbackLabel.Text = "Scanning for nearby BLE devices...";
            UpdateEmptyStates();
        });
    }

    private async void OnStopScanClicked(object? sender, RoutedEventArgs e)
    {
        await RunBusyAsync("Stopping scan...", async () =>
        {
            if (_adapter is null)
            {
                return;
            }

            await _adapter.StopScanAsync();
            FeedbackLabel.Text = _latestScanResults.Count == 0
                ? "Scan stopped. No devices found yet."
                : $"Scan stopped. Found {_latestScanResults.Count} device(s).";
        });
    }

    private async void OnConnectToScanResultClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: ScanResultItem item })
        {
            return;
        }

        await ConnectToDeviceAsync(item.Address, item.DisplayName);
    }

    private void OnRefreshConnectedClicked(object? sender, RoutedEventArgs e)
    {
        RefreshConnectedDevices();
        UpdateStatus();
        FeedbackLabel.Text = _connectedDevices.Count == 0
            ? "No devices are currently connected."
            : $"Loaded {_connectedDevices.Count} connected device(s).";
    }

    private void OnInspectConnectedDeviceClicked(object? sender, RoutedEventArgs e)
    {
        if (_adapter is null || sender is not Button { DataContext: ConnectedDeviceItem item })
        {
            return;
        }

        _selectedDevice = _adapter.ConnectedDevices.FirstOrDefault(device => device.Id == item.Address);
        _serviceLines.Clear();
        UpdateSelectedDeviceDetails();
        FeedbackLabel.Text = _selectedDevice is null
            ? "The selected device is no longer connected."
            : $"Selected {_selectedDevice.Name ?? _selectedDevice.Id}.";
        UpdateEmptyStates();
    }

    private async void OnLoadServicesClicked(object? sender, RoutedEventArgs e)
    {
        if (_selectedDevice is null)
        {
            FeedbackLabel.Text = "Select a connected device first.";
            return;
        }

        await RunBusyAsync("Discovering GATT services...", async () =>
        {
            var services = await _selectedDevice.GetServicesAsync();
            _serviceLines.Clear();

            if (services.Count == 0)
            {
                FeedbackLabel.Text = "No GATT services were found on the selected device.";
                UpdateEmptyStates();
                return;
            }

            foreach (var service in services)
            {
                _serviceLines.Add($"Service: {service.Uuid.ToString().ToUpperInvariant()}");
                var characteristics = await service.GetCharacteristicsAsync();
                foreach (var characteristic in characteristics)
                {
                    _serviceLines.Add($"  • {characteristic.Uuid.ToString().ToUpperInvariant()} [{BuildFlags(characteristic)}]");
                }
            }

            FeedbackLabel.Text = $"Loaded {services.Count} service(s) from {_selectedDevice.Name ?? _selectedDevice.Id}.";
            UpdateEmptyStates();
        });
    }

    private async void OnDisconnectClicked(object? sender, RoutedEventArgs e)
    {
        if (_selectedDevice is null)
        {
            FeedbackLabel.Text = "Select a connected device first.";
            return;
        }

        var device = _selectedDevice;

        await RunBusyAsync($"Disconnecting from {device.Name ?? device.Id}...", async () =>
        {
            await device.DisconnectAsync();
            device.Dispose();

            if (ReferenceEquals(_selectedDevice, device))
            {
                _selectedDevice = null;
                _serviceLines.Clear();
            }

            RefreshConnectedDevices();
            UpdateSelectedDeviceDetails();
            FeedbackLabel.Text = "Disconnected from device.";
            UpdateEmptyStates();
        });
    }

    private async Task<bool> RequestAccessAsync()
    {
        if (_adapter is null)
        {
            PermissionStatusLabel.Text = "Unavailable";
            FeedbackLabel.Text = "BLE is not supported on this platform.";
            UpdateStatus();
            return false;
        }

        var access = await _requestBluetoothAccessAsync();
        var message = access ? "BLE access granted." : "BLE access denied.";

        PermissionStatusLabel.Text = message;
        UpdateStatus();

        if (!access)
        {
            FeedbackLabel.Text = message;
        }

        return access;
    }

    private async Task ConnectToDeviceAsync(string address, string displayName)
    {
        if (_adapter is null || !await RequestAccessAsync())
        {
            return;
        }

        await RunBusyAsync($"Connecting to {displayName}...", async () =>
        {
            _selectedDevice = await _adapter.ConnectAsync(address, new ConnectionConfig
            {
                ConnectionTimeout = TimeSpan.FromSeconds(15)
            });

            _serviceLines.Clear();
            RefreshConnectedDevices();
            UpdateSelectedDeviceDetails();
            FeedbackLabel.Text = $"Connected to {_selectedDevice.Name ?? _selectedDevice.Id}.";
            UpdateEmptyStates();
        });
    }

    private void RebuildScanResults()
    {
        var ordered = _latestScanResults.Values
            .OrderByDescending(result => result.Rssi)
            .Select(result => new ScanResultItem(
                string.IsNullOrWhiteSpace(result.Name) ? "(unknown)" : result.Name!,
                result.Address,
                result.Rssi,
                result.IsConnectable))
            .ToList();

        _scanResults.Clear();
        foreach (var result in ordered)
        {
            _scanResults.Add(result);
        }

        UpdateEmptyStates();
    }

    private void RefreshConnectedDevices()
    {
        if (_adapter is null)
        {
            _connectedDevices.Clear();
            UpdateEmptyStates();
            return;
        }

        var snapshot = _adapter.ConnectedDevices
            .Select(device => new ConnectedDeviceItem(
                string.IsNullOrWhiteSpace(device.Name) ? "(unknown)" : device.Name!,
                device.Id,
                device.State.ToString()))
            .ToList();

        _connectedDevices.Clear();
        foreach (var device in snapshot)
        {
            _connectedDevices.Add(device);
        }

        if (_selectedDevice is not null)
        {
            _selectedDevice = _adapter.ConnectedDevices.FirstOrDefault(device => device.Id == _selectedDevice.Id);
        }

        UpdateEmptyStates();
    }

    private void UpdateStatus()
    {
        AdapterStateLabel.Text = _adapter?.AdapterState.ToString() ?? "Unavailable";
        LibraryStateLabel.Text = _adapter?.LibraryState.ToString() ?? "Unavailable";
        ConnectedCountLabel.Text = _adapter?.ConnectedDevices.Count.ToString() ?? "0";

        var isScanning = _adapter?.LibraryState == BleLibraryState.Scanning;
        var canInspectSelectedDevice = _selectedDevice?.State == BleDeviceState.Connected;
        var isSupported = _adapter is not null;

        ScanButton.IsEnabled = isSupported && !_isBusy && !isScanning;
        StopScanButton.IsEnabled = isSupported && !_isBusy && isScanning;
        LoadServicesButton.IsEnabled = isSupported && !_isBusy && canInspectSelectedDevice;
        DisconnectButton.IsEnabled = isSupported && !_isBusy && canInspectSelectedDevice;
    }

    private void UpdateSelectedDeviceDetails()
    {
        SelectedDeviceLabel.Text = _selectedDevice?.Name ?? _selectedDevice?.Id ?? "None";
        SelectedDeviceStateLabel.Text = _selectedDevice is null
            ? "None"
            : $"{_selectedDevice.Name ?? _selectedDevice.Id} [{_selectedDevice.State}]";

        if (_selectedDevice is not null && _latestScanResults.TryGetValue(_selectedDevice.Id, out var result))
        {
            SignalLabel.Text = BuildSignalSummary(result.Rssi);
        }
        else
        {
            SignalLabel.Text = "Unavailable";
        }

        UpdateStatus();
    }

    private void UpdateEmptyStates()
    {
        NoScanResultsLabel.IsVisible = _scanResults.Count == 0;
        NoConnectedDevicesLabel.IsVisible = _connectedDevices.Count == 0;
        NoServicesLabel.IsVisible = _serviceLines.Count == 0;
    }

    private async Task RunBusyAsync(string message, Func<Task> operation)
    {
        if (_isBusy)
        {
            return;
        }

        try
        {
            _isBusy = true;
            BusyIndicator.IsVisible = true;
            FeedbackLabel.Text = message;
            UpdateStatus();
            await operation();
        }
        catch (Exception ex)
        {
            FeedbackLabel.Text = ex.Message;
        }
        finally
        {
            _isBusy = false;
            BusyIndicator.IsVisible = false;
            UpdateStatus();
        }
    }

    private static string BuildFlags(IBleCharacteristic characteristic)
    {
        var flags = new List<string>();

        if (characteristic.CanRead)
        {
            flags.Add("Read");
        }

        if (characteristic.CanWrite)
        {
            flags.Add("Write");
        }

        if (characteristic.CanWriteWithoutResponse)
        {
            flags.Add("WriteNoRsp");
        }

        if (characteristic.CanNotify)
        {
            flags.Add("Notify");
        }

        if (characteristic.CanIndicate)
        {
            flags.Add("Indicate");
        }

        return string.Join(", ", flags);
    }

    private static string BuildSignalSummary(int rssi)
    {
        var quality = rssi switch
        {
            >= -55 => "Excellent",
            >= -65 => "Good",
            >= -75 => "Fair",
            >= -85 => "Weak",
            _ => "Very weak"
        };

        return $"{quality} ({rssi} dBm)";
    }

    private sealed record ScanResultItem(string DisplayName, string Address, int Rssi, bool IsConnectable)
    {
        public string SignalSummary => $"{BuildSignalSummary(Rssi)} · {(IsConnectable ? "Connectable" : "Unavailable")}";
    }

    private sealed record ConnectedDeviceItem(string DisplayName, string Address, string State);
}
