# Salar.BluetoothLE

[![NuGet](https://img.shields.io/nuget/v/Salar.BluetoothLE?label=Salar.BluetoothLE)](https://www.nuget.org/packages/Salar.BluetoothLE)
[![NuGet](https://img.shields.io/nuget/v/Salar.BluetoothLE.Maui?label=Salar.BluetoothLE.Maui)](https://www.nuget.org/packages/Salar.BluetoothLE.Maui)

`Salar.BluetoothLE` is a cross-platform Bluetooth Low Energy (BLE) library for .NET applications. It gives you a clean, task-based API for common central/client workflows such as scanning for nearby devices, connecting, discovering GATT services and characteristics, reading values, writing data, and subscribing to notifications.

Please note that more than 90% of this library created by AI agents.

The library targets modern .NET applications on:

- Android
- iOS
- Linux
- Windows
- .NET MAUI apps through the companion `Salar.BluetoothLE.Maui` package

## Features

- Cross-platform BLE adapter abstraction
- Scan for nearby BLE peripherals with filtering and duplicate control
- Connect and reconnect by device address
- Discover GATT services and characteristics
- Read characteristic values
- Write characteristic values with or without response
- Subscribe to notifications and indications
- Observe adapter, scan, and library state changes
- DI-friendly registration for MAUI applications
- MAUI permission helpers for Bluetooth access flows

## Links

- NuGet: [Salar.BluetoothLE](https://www.nuget.org/packages/Salar.BluetoothLE)
- NuGet: [Salar.BluetoothLE.Maui](https://www.nuget.org/packages/Salar.BluetoothLE.Maui)


Sample Apps
- Sample app: [`samples/BleDemo.Maui`](./samples/BleDemo.Maui)
- Sample console app: [`samples/BleDemo.Console`](./samples/BleDemo.Console)

## Quick guide

### 1. Choose the package

Use the package that matches your app:

- `Salar.BluetoothLE` for the core BLE API
- `Salar.BluetoothLE.Maui` for .NET MAUI convenience APIs such as Bluetooth permission helpers

```bash
dotnet add package Salar.BluetoothLE
```

```bash
dotnet add package Salar.BluetoothLE.Maui
```

### 2. Register the BLE adapter in a MAUI app

If you are using .NET MAUI, register the adapter in `MauiProgram.cs`:

```csharp
using Salar.BluetoothLE;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>();

        builder.Services.AddBluetoothLE();

        return builder.Build();
    }
}
```

### 3. Configure permissions

BLE applications need platform permissions.

- In .NET MAUI, use `PermissionHelper.RequestBluetoothAccess()` from `Salar.BluetoothLE.Maui`.
- On Android, declare Bluetooth permissions in your app project. You can use the sample at [`samples/BleDemo.Maui/Platforms/Android/AndroidPermissions.cs`](./samples/BleDemo.Maui/Platforms/Android/AndroidPermissions.cs) as a starting point.
- On Linux, install BlueZ, make sure the `bluetooth` service is running, and make sure the app can access the system D-Bus Bluetooth service.

#### Linux prerequisites

The Linux implementation talks directly to the system BlueZ service over D-Bus. Before running a Linux app with `Salar.BluetoothLE`, make sure the host is set up correctly:

1. Install the Bluetooth stack packages for your distro.
   - Debian/Ubuntu example:

     ```bash
     sudo apt-get update
     sudo apt-get install -y bluez dbus
     ```

   - Other distros should install their equivalent BlueZ and D-Bus packages.

2. Make sure the Bluetooth daemon is running.

   ```bash
   sudo systemctl enable --now bluetooth
   sudo systemctl status bluetooth
   ```

3. Make sure the machine actually has a Bluetooth adapter and that Linux can see it.

   ```bash
   bluetoothctl list
   ```

   If no adapter is listed, check your hardware, kernel drivers, or USB Bluetooth dongle support first.

4. Make sure Bluetooth is powered on.

   ```bash
   bluetoothctl power on
   ```

5. Run the app in an environment that can access the system D-Bus.
   - On a normal Linux host, this usually just means running as a user that can talk to the system bus.
   - In containers, minimal VMs, or CI environments, BlueZ and the system D-Bus socket may be missing even if the app itself builds successfully.

If Linux setup is incomplete, the library may report unavailable access or fail to find any Bluetooth adapters.

### 4. Quick start: scan, connect, and write data

The example below shows a typical BLE flow:

1. request access
2. scan for a device
3. connect to the selected peripheral
4. discover a service and characteristic
5. write bytes to the characteristic

```csharp
using System.Text;
using Salar.BluetoothLE;
using Salar.BluetoothLE.Core.Enums;
using Salar.BluetoothLE.Core.Interfaces;
using Salar.BluetoothLE.Core.Models;

public sealed class BleWorkflow
{
    private readonly IBleAdapter _adapter;

    public BleWorkflow(IBleAdapter adapter)
    {
        _adapter = adapter;
    }

    public async Task SendCommandAsync(Guid serviceUuid, Guid characteristicUuid, CancellationToken cancellationToken = default)
    {
        var access = await _adapter.RequestAccessAsync(cancellationToken);
        if (access != BlePermissionStatus.Granted)
        {
            throw new InvalidOperationException("Bluetooth access was not granted.");
        }

        ScanResult? target = null;
        var scanDuration = TimeSpan.FromSeconds(10);

        using var subscription = _adapter.ScanResultReceived.Subscribe(result =>
        {
            if (result.Name == "My BLE Device")
            {
                target = result;
            }
        });

        await _adapter.StartScanAsync(new ScanConfig
        {
            Duration = scanDuration,
            ScanMode = ScanMode.LowLatency,
            AllowDuplicates = false,
        }, cancellationToken);

        await Task.Delay(scanDuration, cancellationToken);
        await _adapter.StopScanAsync(cancellationToken);

        if (target is null)
        {
            throw new InvalidOperationException("Target device not found.");
        }

        using var device = await _adapter.ConnectAsync(target.Address, new ConnectionConfig
        {
            ConnectionTimeout = TimeSpan.FromSeconds(15),
            RequestMtu = 247,
        }, cancellationToken);

        var service = await device.GetServiceAsync(serviceUuid, cancellationToken);
        var characteristic = await service?.GetCharacteristicAsync(characteristicUuid, cancellationToken);

        if (characteristic is null)
        {
            throw new InvalidOperationException("Characteristic not found.");
        }

        if (!characteristic.CanWrite && !characteristic.CanWriteWithoutResponse)
        {
            throw new InvalidOperationException("Characteristic does not support writes.");
        }

        var payload = Encoding.UTF8.GetBytes("hello from Salar.BluetoothLE");
        var writeType = characteristic.CanWrite
            ? WriteType.WithResponse
            : WriteType.WithoutResponse;

        await characteristic.WriteAsync(payload, writeType, cancellationToken);
    }
}
```

## Library reference

This section highlights the most important API surface for day-to-day use.

### `IBleAdapter`

`IBleAdapter` is the main entry point for BLE operations.

**Properties**

- `AdapterState`: current Bluetooth adapter state (`PoweredOn`, `PoweredOff`, `Unauthorized`, ...)
- `LibraryState`: current library state (`Idle`, `Scanning`, `Connecting`)
- `ConnectedDevices`: currently connected BLE devices

**Observables**

- `AdapterStateChanged`
- `ScanResultReceived`
- `LibraryStateChanged`

**Key methods**

- `RequestAccessAsync()`
- `StartScanAsync(ScanConfig? config = null)`
- `StopScanAsync()`
- `ConnectAsync(string address, ConnectionConfig? config = null)`
- `ReconnectAsync(string address, ConnectionConfig? config = null)`

Example:

```csharp
using var subscription = adapter.ScanResultReceived.Subscribe(result =>
{
    Console.WriteLine($"{result.Name} | {result.Address} | RSSI {result.Rssi}");
});

await adapter.StartScanAsync(new ScanConfig
{
    Duration = TimeSpan.FromSeconds(5),
    ServiceUuidFilters = { serviceUuid },
    ScanMode = ScanMode.Balanced,
});

await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
await adapter.StopScanAsync(cancellationToken);
```

### `ScanConfig`

Use `ScanConfig` to control scan behavior:

- `Duration`: how long the scan should run
- `ServiceUuidFilters`: optional service UUID filters
- `ScanMode`: `LowPower`, `Balanced`, `LowLatency`, `Opportunistic`
- `AllowDuplicates`: whether repeated advertisements should be emitted
- `AndroidLegacyScan`: Android-specific legacy mode toggle

### `IBleDevice`

`IBleDevice` represents a connected peripheral.

**Key members**

- `Id`
- `Name`
- `State`
- `Mtu`
- `GetServicesAsync()`
- `GetServiceAsync(Guid serviceUuid)`
- `DisconnectAsync()`
- `RequestMtuAsync(int mtu)`

Example:

```csharp
var services = await device.GetServicesAsync(cancellationToken);

foreach (var service in services)
{
    Console.WriteLine($"Service: {service.Uuid}");
}
```

### `IBleService`

`IBleService` lets you inspect the GATT services exposed by a connected device.

- `Uuid`
- `GetCharacteristicsAsync()`
- `GetCharacteristicAsync(Guid characteristicUuid)`

### `IBleCharacteristic`

`IBleCharacteristic` represents a GATT characteristic and exposes capability flags:

- `CanRead`
- `CanWrite`
- `CanWriteWithoutResponse`
- `CanNotify`
- `CanIndicate`

**Important operations**

- `ReadAsync()`
- `WriteAsync(byte[] data, WriteType writeType = WriteType.WithResponse)`
- `StartNotificationsAsync(Action<byte[]> handler)`
- `StopNotificationsAsync()`

Example:

```csharp
var value = await characteristic.ReadAsync(cancellationToken);

await characteristic.StartNotificationsAsync(data =>
{
    Console.WriteLine(BitConverter.ToString(data));
}, cancellationToken);
```

### `ConnectionConfig`

Use `ConnectionConfig` to tune connection behavior:

- `AutoConnect`
- `RequestMtu`
- `ConnectionTimeout`

Example:

```csharp
var device = await adapter.ConnectAsync(address, new ConnectionConfig
{
    AutoConnect = false,
    RequestMtu = 247,
    ConnectionTimeout = TimeSpan.FromSeconds(15),
}, cancellationToken);
```

### `PermissionHelper` (`Salar.BluetoothLE.Maui`)

If you are building a MAUI app and reference `Salar.BluetoothLE.Maui`, use:

- `PermissionHelper.CheckBluetoothStatus()`
- `PermissionHelper.RequestBluetoothAccess()`

Example:

```csharp
using Salar.BluetoothLE.Maui;

var hasAccess = await PermissionHelper.RequestBluetoothAccess();
if (!hasAccess)
{
    await DisplayAlert("Bluetooth", "Bluetooth permission is required.", "OK");
}
```

## Notes

- The library is focused on BLE central/client scenarios.
- For end-to-end examples, see the MAUI and console samples included in this repository.
- On Android and iOS, always verify your app manifest and runtime permission flow before shipping.
