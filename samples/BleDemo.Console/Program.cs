using BleDemo.Console.UI;
using Salar.BluetoothLE.Core.Enums;
using Salar.BluetoothLE.Core.Interfaces;
using Salar.BluetoothLE.Core.Models;
using Salar.BluetoothLE.Windows;

// ── Bootstrap ─────────────────────────────────────────────────────────────────
// WindowsBleAdapter uses the Windows 10+ WinRT Bluetooth LE APIs to scan for
// and connect to real BLE hardware.  Run this app on a Windows machine with a
// Bluetooth adapter to interact with physical BLE devices.
using var adapter = new WindowsBleAdapter();

// Discovered devices accumulated during the scan
var scanResults = new Dictionary<string, ScanResult>(StringComparer.OrdinalIgnoreCase);

// Subscribe to scan results
using var scanSub = adapter.ScanResultReceived.Subscribe(r =>
{
    scanResults[r.Address] = r;   // keep newest result per address
});

// ── Main loop ─────────────────────────────────────────────────────────────────
System.Console.Title = "BLE Demo";
ConsoleUi.PrintHeader("BLE Library — Interactive Console Demo");
ConsoleUi.Dim("Adapter: WindowsBleAdapter  (real BLE hardware via Windows 10+ WinRT APIs)");

while (true)
{
    await ShowMainMenuAsync();
}

// ── Screens ───────────────────────────────────────────────────────────────────

async Task ShowMainMenuAsync()
{
    ConsoleUi.PrintHeader("Main Menu");

    // Show live adapter / library state in the header
    var adapterColor = adapter.AdapterState == BleAdapterState.PoweredOn
        ? ConsoleColor.Green : ConsoleColor.Red;
    System.Console.Write("  Bluetooth: ");
    ConsoleUi.Write(adapter.AdapterState.ToString(), adapterColor);
    System.Console.Write("   Library: ");
    ConsoleUi.WriteLine(adapter.LibraryState.ToString(), ConsoleColor.Yellow);
    System.Console.WriteLine();

    int choice = ConsoleUi.PromptMenu("What would you like to do?",
    [
        "Scan for BLE devices",
        $"Connected devices ({adapter.ConnectedDevices.Count})",
        "Adapter status",
        "Exit",
    ]);

    switch (choice)
    {
        case 1: await ScanAndConnectAsync(); break;
        case 2: await ShowConnectedDevicesAsync(); break;
        case 3: ShowAdapterStatus(); break;
        case 4:
            ConsoleUi.Info("Cleaning up and exiting...");
            await adapter.StopScanAsync();
            foreach (var d in adapter.ConnectedDevices.ToList())
                await d.DisconnectAsync();
            Environment.Exit(0);
            break;
        default:
            ConsoleUi.Error("Invalid selection.");
            break;
    }
}

// ── Scan screen ───────────────────────────────────────────────────────────────

async Task ScanAndConnectAsync()
{
    ConsoleUi.PrintHeader("Scan for BLE Devices");

    var scanConfig = new ScanConfig
    {
        Duration        = TimeSpan.FromSeconds(10),
        ScanMode        = ScanMode.LowPower,
        AllowDuplicates = false,
    };

    ConsoleUi.Info($"Starting scan ({scanConfig.Duration.TotalSeconds:0}s, mode: {scanConfig.ScanMode})...");
    scanResults.Clear();

    int previousCount = 0;

    // Start scan then show a live-updating device list
    await adapter.StartScanAsync(scanConfig);

    // Poll until scanning finishes or user presses [S] to stop early
    System.Console.WriteLine();
    ConsoleUi.Dim("  (press [S] to stop scan early)");
    System.Console.WriteLine();

    var keyTask = Task.Run(() =>
    {
        // KeyAvailable throws when stdin is redirected; skip interactive stop in that case
        if (System.Console.IsInputRedirected) return;
        while (true)
        {
            try
            {
                if (System.Console.KeyAvailable)
                {
                    var k = System.Console.ReadKey(intercept: true);
                    if (k.Key == ConsoleKey.S) return;
                }
            }
            catch (InvalidOperationException) { return; }
            Thread.Sleep(100);
        }
    });

    // Live display loop
    while (adapter.LibraryState == BleLibraryState.Scanning && !keyTask.IsCompleted)
    {
        if (scanResults.Count != previousCount)
        {
            previousCount = scanResults.Count;
            PrintScanResults(scanResults.Values.ToList());
        }
        await Task.Delay(300);
    }

    if (adapter.LibraryState == BleLibraryState.Scanning)
        await adapter.StopScanAsync();

    PrintScanResults(scanResults.Values.ToList());

    if (scanResults.Count == 0)
    {
        ConsoleUi.Error("No devices found.");
        ConsoleUi.PressAnyKey();
        return;
    }

    // Let user pick a device to connect to
    await SelectAndConnectAsync(scanResults.Values.OrderByDescending(r => r.Rssi).ToList());
}

void PrintScanResults(IReadOnlyList<ScanResult> results)
{
    // Move cursor up to overwrite previous list
    System.Console.Write("\r");

    if (results.Count == 0)
    {
        ConsoleUi.Dim("  Searching...");
        return;
    }

    System.Console.WriteLine($"  Found {results.Count} device(s):");
    ConsoleUi.Separator();

    int idx = 1;
    foreach (var r in results.OrderByDescending(x => x.Rssi))
    {
        string name = string.IsNullOrWhiteSpace(r.Name) ? "(unknown)" : r.Name;
        System.Console.Write($"  {idx++,2}. ");
        ConsoleUi.Write($"{name,-25}", ConsoleColor.White);
        System.Console.Write("  ");
        ConsoleUi.Write(ConsoleUi.SignalBar(r.Rssi), RssiColor(r.Rssi));
        System.Console.WriteLine();
        ConsoleUi.Dim($"       {r.Address}  connectable:{r.IsConnectable}");
    }

    ConsoleUi.Separator();
}

async Task SelectAndConnectAsync(IReadOnlyList<ScanResult> results)
{
    var options = results
        .Select(r => $"{(string.IsNullOrWhiteSpace(r.Name) ? "(unknown)" : r.Name),-25}  {r.Address}  {ConsoleUi.SignalBar(r.Rssi)}")
        .Append("<- Back to main menu")
        .ToList();

    int choice = ConsoleUi.PromptMenu("Select a device to connect:", options);

    if (choice == 0 || choice == options.Count)
        return;   // back

    var selected = results[choice - 1];
    ConsoleUi.Info($"Connecting to \"{selected.Name ?? selected.Address}\"...");

    IBleDevice? device = null;
    try
    {
        await ConsoleUi.SpinAsync("Connecting...", Task.Run(async () =>
        {
            device = await adapter.ConnectAsync(
                selected.Address,
                new ConnectionConfig { ConnectionTimeout = TimeSpan.FromSeconds(15) });
        }));

        ConsoleUi.Success($"Connected to {device!.Name ?? device.Id}  (MTU {device.Mtu})");
        await DeviceMenuAsync(device, selected);
    }
    catch (Exception ex)
    {
        ConsoleUi.Error($"Connection failed: {ex.Message}");
        ConsoleUi.PressAnyKey();
    }
}

// ── Device menu ───────────────────────────────────────────────────────────────

async Task DeviceMenuAsync(IBleDevice device, ScanResult? scanResult)
{
    while (true)
    {
        ConsoleUi.PrintHeader($"Device - {device.Name ?? device.Id}");

        System.Console.Write("  Status: ");
        ConsoleUi.WriteLine(device.State.ToString(),
            device.State == BleDeviceState.Connected ? ConsoleColor.Green : ConsoleColor.Red);
        System.Console.WriteLine();

        int choice = ConsoleUi.PromptMenu("Options:",
        [
            "Check signal strength (RSSI)",
            "List GATT services",
            "Disconnect",
            "<- Back to main menu",
        ]);

        switch (choice)
        {
            case 1:
                CheckSignal(device, scanResult);
                break;

            case 2:
                await ListServicesAsync(device);
                break;

            case 3:
                await DisconnectAsync(device);
                return;   // go back to main menu

            case 4:
            case 0:
                return;   // back without disconnect

            default:
                ConsoleUi.Error("Invalid selection.");
                break;
        }

        if (device.State != BleDeviceState.Connected)
        {
            ConsoleUi.Dim("Device is no longer connected.");
            ConsoleUi.PressAnyKey();
            return;
        }
    }
}

// ── Check signal ──────────────────────────────────────────────────────────────

void CheckSignal(IBleDevice device, ScanResult? lastScan)
{
    ConsoleUi.PrintHeader("Signal Strength");

    // The RSSI shown here comes from the last advertisement received during
    // scanning.  WinRT does not expose a live ReadRSSI call on a connected
    // GATT session, so the scan-time value is the best available signal data.
    if (lastScan is not null)
    {
        int rssi = lastScan.Rssi;
        System.Console.Write("  RSSI (last scan): ");
        ConsoleUi.WriteLine(ConsoleUi.SignalBar(rssi), RssiColor(rssi));

        string quality = rssi >= -55 ? "Excellent"
                       : rssi >= -65 ? "Good"
                       : rssi >= -75 ? "Fair"
                       : rssi >= -85 ? "Weak"
                       : "Very weak";
        System.Console.Write("  Quality: ");
        ConsoleUi.WriteLine(quality, RssiColor(rssi));
    }
    else
    {
        ConsoleUi.Dim("  RSSI not available (device was not discovered via scan).");
    }

    System.Console.WriteLine();
    ConsoleUi.PressAnyKey();
}

// ── List GATT services ────────────────────────────────────────────────────────

async Task ListServicesAsync(IBleDevice device)
{
    ConsoleUi.PrintHeader("GATT Services");

    IReadOnlyList<IBleService>? services = null;
    await ConsoleUi.SpinAsync("Discovering services...",
        Task.Run(async () => services = await device.GetServicesAsync()));

    if (services is null || services.Count == 0)
    {
        ConsoleUi.Dim("  No services found.");
    }
    else
    {
        foreach (var svc in services)
        {
            ConsoleUi.Write("  * ", ConsoleColor.DarkCyan);
            ConsoleUi.WriteLine(svc.Uuid.ToString().ToUpperInvariant(), ConsoleColor.White);
            var chars = await svc.GetCharacteristicsAsync();
            foreach (var ch in chars)
            {
                string flags = string.Join(", ", new[]
                {
                    ch.CanRead                  ? "Read"       : null,
                    ch.CanWrite                 ? "Write"      : null,
                    ch.CanWriteWithoutResponse  ? "WriteNoRsp" : null,
                    ch.CanNotify                ? "Notify"     : null,
                    ch.CanIndicate              ? "Indicate"   : null,
                }.Where(f => f is not null)!);

                ConsoleUi.Dim($"      o {ch.Uuid.ToString().ToUpperInvariant()}  [{flags}]");
            }
        }
    }

    System.Console.WriteLine();
    ConsoleUi.PressAnyKey();
}

// ── Disconnect ────────────────────────────────────────────────────────────────

async Task DisconnectAsync(IBleDevice device)
{
    ConsoleUi.Info($"Disconnecting from {device.Name ?? device.Id}...");
    try
    {
        await ConsoleUi.SpinAsync("Disconnecting...", device.DisconnectAsync());
        ConsoleUi.Success("Disconnected.");
    }
    catch (Exception ex)
    {
        ConsoleUi.Error($"Error during disconnect: {ex.Message}");
    }
    device.Dispose();
    System.Console.WriteLine();
    ConsoleUi.PressAnyKey();
}

// ── Connected devices screen ──────────────────────────────────────────────────

async Task ShowConnectedDevicesAsync()
{
    ConsoleUi.PrintHeader("Connected Devices");

    var devices = adapter.ConnectedDevices.ToList();
    if (devices.Count == 0)
    {
        ConsoleUi.Dim("  No devices currently connected.");
        ConsoleUi.PressAnyKey();
        return;
    }

    var options = devices
        .Select(d => $"{d.Name ?? "(unknown)",-25}  {d.Id}  [{d.State}]")
        .Append("<- Back")
        .ToList();

    int choice = ConsoleUi.PromptMenu("Select a connected device:", options);
    if (choice == 0 || choice == options.Count) return;

    var selected = devices[choice - 1];
    // Re-use the last scan result for this device so RSSI is available in the
    // device menu even when navigating from the "Connected devices" list rather
    // than from a fresh scan.
    scanResults.TryGetValue(selected.Id, out var lastScan);
    await DeviceMenuAsync(selected, lastScan);
}

// ── Adapter status screen ─────────────────────────────────────────────────────

void ShowAdapterStatus()
{
    ConsoleUi.PrintHeader("Adapter Status");

    var adapterColor = adapter.AdapterState == BleAdapterState.PoweredOn
        ? ConsoleColor.Green : ConsoleColor.Red;

    System.Console.Write("  Adapter state:  ");
    ConsoleUi.WriteLine(adapter.AdapterState.ToString(), adapterColor);

    System.Console.Write("  Library state:  ");
    ConsoleUi.WriteLine(adapter.LibraryState.ToString(), ConsoleColor.Yellow);

    System.Console.Write("  Connected count: ");
    ConsoleUi.WriteLine(adapter.ConnectedDevices.Count.ToString(), ConsoleColor.Cyan);

    System.Console.WriteLine();
    ConsoleUi.PressAnyKey();
}

// ── Helpers ───────────────────────────────────────────────────────────────────

static ConsoleColor RssiColor(int rssi) =>
    rssi >= -65 ? ConsoleColor.Green
  : rssi >= -75 ? ConsoleColor.Yellow
  : ConsoleColor.Red;

