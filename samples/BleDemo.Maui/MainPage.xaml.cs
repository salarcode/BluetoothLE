using System.Collections.ObjectModel;
using Salar.BluetoothLE.Core.Enums;
using Salar.BluetoothLE.Core.Interfaces;
using Salar.BluetoothLE.Core.Models;
using Microsoft.Maui.ApplicationModel;

namespace BleDemo.Maui;

public partial class MainPage : ContentPage
{
	private readonly IBleAdapter _adapter;
	private readonly ObservableCollection<ScanResultItem> _scanResults = [];
	private readonly ObservableCollection<ConnectedDeviceItem> _connectedDevices = [];
	private readonly ObservableCollection<string> _serviceLines = [];
	private readonly Dictionary<string, ScanResult> _latestScanResults = new(StringComparer.OrdinalIgnoreCase);
	private readonly IDisposable _scanSubscription;
	private readonly IDisposable _adapterStateSubscription;
	private readonly IDisposable _libraryStateSubscription;
	private IBleDevice? _selectedDevice;
	private bool _isBusy;

	public MainPage(IBleAdapter adapter)
	{
		InitializeComponent();

		_adapter = adapter;
		ScanResultsView.ItemsSource = _scanResults;
		ConnectedDevicesView.ItemsSource = _connectedDevices;
		ServicesView.ItemsSource = _serviceLines;

		_scanSubscription = _adapter.ScanResultReceived.Subscribe(OnScanResultReceived);
		_adapterStateSubscription = _adapter.AdapterStateChanged.Subscribe(_ => MainThread.BeginInvokeOnMainThread(UpdateStatus));
		_libraryStateSubscription = _adapter.LibraryStateChanged.Subscribe(state => MainThread.BeginInvokeOnMainThread(() => OnLibraryStateChanged(state)));

		RefreshConnectedDevices();
		UpdateStatus();
		UpdateSelectedDeviceDetails();
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		RefreshConnectedDevices();
		UpdateStatus();
	}

	private void OnScanResultReceived(ScanResult result)
	{
		MainThread.BeginInvokeOnMainThread(() =>
		{
			_latestScanResults[result.Address] = result;
			RebuildScanResults();

			if (_selectedDevice?.Id == result.Address)
			{
				SignalLabel.Text = BuildSignalSummary(result.Rssi);
			}
		});
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

	private async void OnRequestAccessClicked(object? sender, EventArgs e)
	{
		await RequestAccessAsync();
	}

	private async void OnScanClicked(object? sender, EventArgs e)
	{
		if (!await RequestAccessAsync())
		{
			return;
		}

		await RunBusyAsync("Starting BLE scan...", async () =>
		{
			_latestScanResults.Clear();
			_scanResults.Clear();
			await _adapter.StartScanAsync(new ScanConfig
			{
				Duration = TimeSpan.FromSeconds(10),
				ScanMode = ScanMode.LowPower,
				AllowDuplicates = false,
				AndroidScanMode = AndroidScanMode.Auto,
			});
			FeedbackLabel.Text = "Scanning for nearby BLE devices...";
		});
	}

	private async void OnStopScanClicked(object? sender, EventArgs e)
	{
		await RunBusyAsync("Stopping scan...", async () =>
		{
			await _adapter.StopScanAsync();
			FeedbackLabel.Text = _latestScanResults.Count == 0
				? "Scan stopped. No devices found yet."
				: $"Scan stopped. Found {_latestScanResults.Count} device(s).";
		});
	}

	private async void OnConnectToScanResultClicked(object? sender, EventArgs e)
	{
		if (sender is not Button { BindingContext: ScanResultItem item })
		{
			return;
		}

		await ConnectToDeviceAsync(item.Address, item.DisplayName);
	}

	private void OnRefreshConnectedClicked(object? sender, EventArgs e)
	{
		RefreshConnectedDevices();
		UpdateStatus();
		FeedbackLabel.Text = _connectedDevices.Count == 0
			? "No devices are currently connected."
			: $"Loaded {_connectedDevices.Count} connected device(s).";
	}

	private void OnInspectConnectedDeviceClicked(object? sender, EventArgs e)
	{
		if (sender is not Button { BindingContext: ConnectedDeviceItem item })
		{
			return;
		}

		_selectedDevice = _adapter.ConnectedDevices.FirstOrDefault(device => device.Id == item.Address);
		_serviceLines.Clear();
		UpdateSelectedDeviceDetails();
		FeedbackLabel.Text = _selectedDevice is null
			? "The selected device is no longer connected."
			: $"Selected {_selectedDevice.Name ?? _selectedDevice.Id}.";
	}

	private async void OnLoadServicesClicked(object? sender, EventArgs e)
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
		});
	}

	private async void OnDisconnectClicked(object? sender, EventArgs e)
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
		});
	}

	private async Task<bool> RequestAccessAsync()
	{
		var access = await PermissionHelper.RequestBluetoothAccess();
		var message = access ? "BLE access granted." : "BLE access denied.";


		PermissionStatusLabel.Text = message;
		UpdateStatus();

		if (!access)
		{
			FeedbackLabel.Text = message;
			_ = DisplayAlertAsync("BLE Access", message, "OK");
		}

		return access;
	}

	private async Task ConnectToDeviceAsync(string address, string displayName)
	{
		if (!await RequestAccessAsync())
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
	}

	private void RefreshConnectedDevices()
	{
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
	}

	private void UpdateStatus()
	{
		AdapterStateLabel.Text = _adapter.AdapterState.ToString();
		LibraryStateLabel.Text = _adapter.LibraryState.ToString();
		ConnectedCountLabel.Text = _adapter.ConnectedDevices.Count.ToString();
		StopScanButton.IsEnabled = !_isBusy && _adapter.LibraryState == BleLibraryState.Scanning;
		ScanButton.IsEnabled = !_isBusy && _adapter.LibraryState != BleLibraryState.Scanning;
		LoadServicesButton.IsEnabled = !_isBusy && _selectedDevice?.State == BleDeviceState.Connected;
		DisconnectButton.IsEnabled = !_isBusy && _selectedDevice?.State == BleDeviceState.Connected;
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
			BusyIndicator.IsRunning = true;
			FeedbackLabel.Text = message;
			UpdateStatus();
			await operation();
		}
		catch (Exception ex)
		{
			FeedbackLabel.Text = ex.Message;
			await DisplayAlert("BLE Demo", ex.Message, "OK");
		}
		finally
		{
			_isBusy = false;
			BusyIndicator.IsVisible = false;
			BusyIndicator.IsRunning = false;
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

