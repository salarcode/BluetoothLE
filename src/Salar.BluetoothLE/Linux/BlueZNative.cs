using System.Runtime.CompilerServices;
using Tmds.DBus;

[assembly: InternalsVisibleTo(Tmds.DBus.Connection.DynamicAssemblyName)]

namespace Salar.BluetoothLE.Linux.BlueZ;

internal static class BlueZConstants
{
    public const string ServiceName = "org.bluez";
    public const string AdapterInterface = "org.bluez.Adapter1";
    public const string DeviceInterface = "org.bluez.Device1";
    public const string GattServiceInterface = "org.bluez.GattService1";
    public const string GattCharacteristicInterface = "org.bluez.GattCharacteristic1";
}

internal delegate Task AdapterEventHandlerAsync(Adapter sender, BlueZEventArgs eventArgs);
internal delegate Task DeviceChangeEventHandlerAsync(Adapter sender, DeviceFoundEventArgs eventArgs);
internal delegate Task DeviceEventHandlerAsync(Device sender, BlueZEventArgs eventArgs);
internal delegate Task GattCharacteristicEventHandlerAsync(GattCharacteristic sender, GattCharacteristicValueEventArgs eventArgs);

internal sealed class BlueZEventArgs(bool isStateChange) : EventArgs
{
    public bool IsStateChange { get; } = isStateChange;
}

internal sealed class DeviceFoundEventArgs(Device device, bool isStateChange) : EventArgs
{
    public Device Device { get; } = device;
    public bool IsStateChange { get; } = isStateChange;
}

internal sealed class GattCharacteristicValueEventArgs(byte[] value) : EventArgs
{
    public byte[] Value { get; } = value;
}

internal static class BlueZManager
{
    private static readonly ObjectPath RootPath = new("/");

    public static async Task<IReadOnlyList<Adapter>> GetAdaptersAsync()
    {
        await Connection.System.ConnectAsync();

        var managedObjects = await GetManagedObjectsAsync();
        var adapterPaths = managedObjects
            .Where(entry => entry.Value.ContainsKey(BlueZConstants.AdapterInterface))
            .Select(entry => entry.Key)
            .OrderBy(path => path.ToString(), StringComparer.Ordinal)
            .ToList();

        var result = new List<Adapter>(adapterPaths.Count);
        foreach (var path in adapterPaths)
            result.Add(await Adapter.CreateAsync(path));

        return result.AsReadOnly();
    }

    public static string NormalizeUUID(string uuid)
    {
        if (string.IsNullOrWhiteSpace(uuid))
            throw new ArgumentException("UUID value cannot be empty.", nameof(uuid));

        var normalized = uuid.Trim();

        if (Guid.TryParse(normalized, out var parsed))
            return parsed.ToString("D");

        if (normalized.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            normalized = normalized[2..];

        if (normalized.Length == 4 && ushort.TryParse(normalized, System.Globalization.NumberStyles.HexNumber, null, out _))
            normalized = $"0000{normalized}-0000-1000-8000-00805f9b34fb";
        else if (normalized.Length == 8 && uint.TryParse(normalized, System.Globalization.NumberStyles.HexNumber, null, out _))
            normalized = $"{normalized}-0000-1000-8000-00805f9b34fb";

        if (!Guid.TryParse(normalized, out parsed))
            throw new FormatException($"'{uuid}' is not a valid Bluetooth UUID.");

        return parsed.ToString("D");
    }

    public static async Task<IDictionary<ObjectPath, IDictionary<string, IDictionary<string, object>>>> GetManagedObjectsAsync()
    {
        var objectManager = Connection.System.CreateProxy<IObjectManager>(BlueZConstants.ServiceName, RootPath);
        return await objectManager.GetManagedObjectsAsync();
    }

    public static T CreateProxy<T>(ObjectPath path)
        where T : IDBusObject
        => Connection.System.CreateProxy<T>(BlueZConstants.ServiceName, path);

    public static bool IsChildPath(ObjectPath path, ObjectPath parent, string requiredPrefix)
    {
        var parentValue = parent.ToString();
        var value = path.ToString();
        return value.StartsWith(parentValue + "/" + requiredPrefix, StringComparison.Ordinal);
    }
}

internal sealed class Adapter : IDisposable
{
    private readonly IAdapter1 _proxy;
    private readonly IObjectManager _objectManager;

    private IDisposable? _propertiesWatcher;
    private IDisposable? _interfacesAddedWatcher;
    private bool _disposed;

    private Adapter(ObjectPath objectPath)
    {
        ObjectPath = objectPath;
        _proxy = BlueZManager.CreateProxy<IAdapter1>(objectPath);
        _objectManager = BlueZManager.CreateProxy<IObjectManager>(new ObjectPath("/"));
    }

    public ObjectPath ObjectPath { get; }
    public string Name => ObjectPath.ToString().Split('/').Last();

    public event DeviceChangeEventHandlerAsync? DeviceFound;
    public event AdapterEventHandlerAsync? PoweredOn;
    public event AdapterEventHandlerAsync? PoweredOff;

    public static async Task<Adapter> CreateAsync(ObjectPath objectPath)
    {
        var adapter = new Adapter(objectPath);
        await adapter.InitializeAsync();
        return adapter;
    }

    private async Task InitializeAsync()
    {
        _propertiesWatcher = await _proxy.WatchPropertiesAsync(OnPropertiesChanged);
        _interfacesAddedWatcher = await _objectManager.WatchInterfacesAddedAsync(OnInterfacesAdded, HandleWatcherError);
    }

    public Task<bool> GetPoweredAsync()
        => _proxy.GetAsync<bool>("Powered");

    public Task StartDiscoveryAsync()
        => _proxy.StartDiscoveryAsync();

    public Task StopDiscoveryAsync()
        => _proxy.StopDiscoveryAsync();

    public async Task<IReadOnlyList<Device>> GetDevicesAsync()
    {
        var managedObjects = await BlueZManager.GetManagedObjectsAsync();
        var devicePaths = managedObjects
            .Where(entry =>
                entry.Value.ContainsKey(BlueZConstants.DeviceInterface) &&
                BlueZManager.IsChildPath(entry.Key, ObjectPath, "dev_"))
            .Select(entry => entry.Key)
            .OrderBy(path => path.ToString(), StringComparer.Ordinal)
            .ToList();

        var result = new List<Device>(devicePaths.Count);
        foreach (var path in devicePaths)
            result.Add(await Device.CreateAsync(path, watchProperties: false));

        return result.AsReadOnly();
    }

    public async Task<Device?> GetDeviceAsync(string address)
    {
        var normalizedAddress = NormalizeAddress(address);
        var devices = await GetDevicesAsync();

        foreach (var device in devices)
        {
            var properties = await device.GetPropertiesAsync();
            if (NormalizeAddress(properties.Address) == normalizedAddress)
            {
                var matchedPath = device.ObjectPath;
                device.Dispose();
                return await Device.CreateAsync(matchedPath, watchProperties: true);
            }

            device.Dispose();
        }

        return null;
    }

    private void OnPropertiesChanged(PropertyChanges changes)
    {
        if (!TryGetChangedValue(changes, "Powered", out bool powered))
            return;

        _ = powered
            ? InvokeAsync(PoweredOn, new BlueZEventArgs(isStateChange: true))
            : InvokeAsync(PoweredOff, new BlueZEventArgs(isStateChange: true));
    }

    private void OnInterfacesAdded((ObjectPath @object, IDictionary<string, IDictionary<string, object>> interfaces) change)
    {
        if (!change.interfaces.ContainsKey(BlueZConstants.DeviceInterface))
            return;

        if (!BlueZManager.IsChildPath(change.@object, ObjectPath, "dev_"))
            return;

        _ = NotifyDeviceFoundAsync(change.@object);
    }

    private async Task NotifyDeviceFoundAsync(ObjectPath objectPath)
    {
        try
        {
            var device = await Device.CreateAsync(objectPath, watchProperties: false);
            await InvokeAsync(DeviceFound, new DeviceFoundEventArgs(device, isStateChange: true));
        }
        catch
        {
        }
    }

    private static void HandleWatcherError(Exception exception)
    {
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _propertiesWatcher?.Dispose();
        _interfacesAddedWatcher?.Dispose();
    }

    private async Task InvokeAsync(AdapterEventHandlerAsync? handlers, BlueZEventArgs args)
    {
        if (handlers == null)
            return;

        foreach (AdapterEventHandlerAsync handler in handlers.GetInvocationList())
            await handler(this, args);
    }

    private async Task InvokeAsync(DeviceChangeEventHandlerAsync? handlers, DeviceFoundEventArgs args)
    {
        if (handlers == null)
            return;

        foreach (DeviceChangeEventHandlerAsync handler in handlers.GetInvocationList())
            await handler(this, args);
    }

    private static bool TryGetChangedValue<T>(PropertyChanges changes, string propertyName, out T value)
    {
        foreach (var (name, changedValue) in changes.Changed)
        {
            if (!string.Equals(name, propertyName, StringComparison.Ordinal))
                continue;

            if (changedValue is T direct)
            {
                value = direct;
                return true;
            }
        }

        value = default!;
        return false;
    }

    private static string NormalizeAddress(string address)
        => string.IsNullOrWhiteSpace(address)
            ? string.Empty
            : address.Replace('-', ':').Trim().ToUpperInvariant();
}

internal sealed class Device : IDisposable
{
    private readonly IDevice1 _proxy;

    private IDisposable? _propertiesWatcher;
    private bool _disposed;

    private Device(ObjectPath objectPath)
    {
        ObjectPath = objectPath;
        _proxy = BlueZManager.CreateProxy<IDevice1>(objectPath);
    }

    public ObjectPath ObjectPath { get; }

    public event DeviceEventHandlerAsync? Connected;
    public event DeviceEventHandlerAsync? Disconnected;

    public static async Task<Device> CreateAsync(ObjectPath objectPath, bool watchProperties)
    {
        var device = new Device(objectPath);
        if (watchProperties)
            await device.EnablePropertyWatchingAsync();

        return device;
    }

    public Task ConnectAsync()
        => _proxy.ConnectAsync();

    public Task DisconnectAsync()
        => _proxy.DisconnectAsync();

    public async Task<DeviceProperties> GetPropertiesAsync()
    {
        var properties = await _proxy.GetAllAsync();
        return new DeviceProperties
        {
            Address = properties.Address,
            Alias = properties.Alias,
            Name = properties.Name,
            Rssi = properties.RSSI,
            UUIDs = properties.UUIDs ?? [],
            IsConnected = properties.Connected,
            ServicesResolved = properties.ServicesResolved,
            ManufacturerData = properties.ManufacturerData ?? new Dictionary<ushort, byte[]>(),
        };
    }

    public async Task WaitForPropertyValueAsync(string propertyName, object expectedValue, TimeSpan timeout)
    {
        var waiter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var watcher = await _proxy.WatchPropertiesAsync(changes =>
        {
            if (TryGetChangedValue(changes, propertyName, out var value) && Equals(value, expectedValue))
                waiter.TrySetResult();
        });

        if (await HasPropertyValueAsync(propertyName, expectedValue))
            return;

        using var timeoutCts = new CancellationTokenSource(timeout);
        using var registration = timeoutCts.Token.Register(() => waiter.TrySetCanceled(timeoutCts.Token));
        await waiter.Task;
    }

    private async Task<bool> HasPropertyValueAsync(string propertyName, object expectedValue)
    {
        var properties = await _proxy.GetAllAsync();
        return propertyName switch
        {
            "Connected" => Equals(properties.Connected, expectedValue),
            "ServicesResolved" => Equals(properties.ServicesResolved, expectedValue),
            _ => false
        };
    }

    public async Task<IReadOnlyList<GattService>> GetServicesAsync()
    {
        var managedObjects = await BlueZManager.GetManagedObjectsAsync();
        var servicePaths = managedObjects
            .Where(entry =>
                entry.Value.ContainsKey(BlueZConstants.GattServiceInterface) &&
                BlueZManager.IsChildPath(entry.Key, ObjectPath, "service"))
            .Select(entry => entry.Key)
            .OrderBy(path => path.ToString(), StringComparer.Ordinal)
            .ToList();

        var result = new List<GattService>(servicePaths.Count);
        foreach (var path in servicePaths)
            result.Add(new GattService(path));

        return result.AsReadOnly();
    }

    public async Task<GattService?> GetServiceAsync(string serviceUuid)
    {
        var normalizedUuid = BlueZManager.NormalizeUUID(serviceUuid);
        var services = await GetServicesAsync();
        foreach (var service in services)
        {
            var properties = await service.GetAllAsync();
            if (string.Equals(BlueZManager.NormalizeUUID(properties.UUID), normalizedUuid, StringComparison.OrdinalIgnoreCase))
                return service;

            service.Dispose();
        }

        return null;
    }

    private async Task EnablePropertyWatchingAsync()
    {
        _propertiesWatcher ??= await _proxy.WatchPropertiesAsync(OnPropertiesChanged);
    }

    private void OnPropertiesChanged(PropertyChanges changes)
    {
        if (TryGetChangedValue(changes, "Connected", out bool connected))
        {
            _ = connected
                ? InvokeAsync(Connected, new BlueZEventArgs(isStateChange: true))
                : InvokeAsync(Disconnected, new BlueZEventArgs(isStateChange: true));
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _propertiesWatcher?.Dispose();
    }

    private async Task InvokeAsync(DeviceEventHandlerAsync? handlers, BlueZEventArgs args)
    {
        if (handlers == null)
            return;

        foreach (DeviceEventHandlerAsync handler in handlers.GetInvocationList())
            await handler(this, args);
    }

    private static bool TryGetChangedValue(PropertyChanges changes, string propertyName, out object? value)
    {
        foreach (var (name, changedValue) in changes.Changed)
        {
            if (string.Equals(name, propertyName, StringComparison.Ordinal))
            {
                value = changedValue;
                return true;
            }
        }

        value = null;
        return false;
    }

    private static bool TryGetChangedValue<T>(PropertyChanges changes, string propertyName, out T value)
    {
        if (TryGetChangedValue(changes, propertyName, out var changedValue) && changedValue is T typed)
        {
            value = typed;
            return true;
        }

        value = default!;
        return false;
    }
}

internal sealed class GattService : IDisposable
{
    private readonly IGattService1 _proxy;

    public GattService(ObjectPath objectPath)
    {
        ObjectPath = objectPath;
        _proxy = BlueZManager.CreateProxy<IGattService1>(objectPath);
    }

    public ObjectPath ObjectPath { get; }

    public Task<GattServiceProperties> GetAllAsync()
        => _proxy.GetAllAsync();

    public async Task<IReadOnlyList<GattCharacteristic>> GetCharacteristicsAsync()
    {
        var managedObjects = await BlueZManager.GetManagedObjectsAsync();
        var characteristicPaths = managedObjects
            .Where(entry =>
                entry.Value.ContainsKey(BlueZConstants.GattCharacteristicInterface) &&
                BlueZManager.IsChildPath(entry.Key, ObjectPath, "char"))
            .Select(entry => entry.Key)
            .OrderBy(path => path.ToString(), StringComparer.Ordinal)
            .ToList();

        return characteristicPaths.Select(path => new GattCharacteristic(path)).ToList().AsReadOnly();
    }

    public async Task<GattCharacteristic?> GetCharacteristicAsync(string characteristicUuid)
    {
        var normalizedUuid = BlueZManager.NormalizeUUID(characteristicUuid);
        var characteristics = await GetCharacteristicsAsync();
        foreach (var characteristic in characteristics)
        {
            var properties = await characteristic.GetAllAsync();
            if (string.Equals(BlueZManager.NormalizeUUID(properties.UUID), normalizedUuid, StringComparison.OrdinalIgnoreCase))
                return characteristic;

            characteristic.Dispose();
        }

        return null;
    }

    public void Dispose()
    {
    }
}

internal sealed class GattCharacteristic : IDisposable
{
    private readonly IGattCharacteristic1 _proxy;

    private IDisposable? _propertiesWatcher;
    private bool _disposed;

    public GattCharacteristic(ObjectPath objectPath)
    {
        ObjectPath = objectPath;
        _proxy = BlueZManager.CreateProxy<IGattCharacteristic1>(objectPath);
    }

    public ObjectPath ObjectPath { get; }

    public event GattCharacteristicEventHandlerAsync? Value;

    public Task<GattCharacteristicProperties> GetAllAsync()
        => _proxy.GetAllAsync();

    public Task<byte[]> ReadValueAsync(IDictionary<string, object> options)
        => _proxy.ReadValueAsync(options);

    public Task WriteValueAsync(byte[] value, IDictionary<string, object> options)
        => _proxy.WriteValueAsync(value, options);

    public async Task StartNotifyAsync()
    {
        _propertiesWatcher ??= await _proxy.WatchPropertiesAsync(OnPropertiesChanged);
        await _proxy.StartNotifyAsync();
    }

    public Task StopNotifyAsync()
        => _proxy.StopNotifyAsync();

    private void OnPropertiesChanged(PropertyChanges changes)
    {
        if (!TryGetChangedValue(changes, "Value", out object? changedValue))
            return;

        var bytes = changedValue switch
        {
            byte[] buffer => buffer,
            IEnumerable<byte> buffer => buffer.ToArray(),
            _ => null
        };

        if (bytes == null)
            return;

        _ = InvokeAsync(Value, new GattCharacteristicValueEventArgs(bytes));
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _propertiesWatcher?.Dispose();
    }

    private async Task InvokeAsync(GattCharacteristicEventHandlerAsync? handlers, GattCharacteristicValueEventArgs args)
    {
        if (handlers == null)
            return;

        foreach (GattCharacteristicEventHandlerAsync handler in handlers.GetInvocationList())
            await handler(this, args);
    }

    private static bool TryGetChangedValue(PropertyChanges changes, string propertyName, out object? value)
    {
        foreach (var (name, changedValue) in changes.Changed)
        {
            if (string.Equals(name, propertyName, StringComparison.Ordinal))
            {
                value = changedValue;
                return true;
            }
        }

        value = null;
        return false;
    }
}

internal sealed class DeviceProperties
{
    public string Address { get; init; } = string.Empty;
    public string? Alias { get; init; }
    public string? Name { get; init; }
    public short Rssi { get; init; }
    public bool IsConnected { get; init; }
    public bool ServicesResolved { get; init; }
    public string[] UUIDs { get; init; } = [];
    public IDictionary<ushort, byte[]> ManufacturerData { get; init; } = new Dictionary<ushort, byte[]>();
}

[DBusInterface("org.freedesktop.DBus.ObjectManager")]
internal interface IObjectManager : IDBusObject
{
    Task<IDictionary<ObjectPath, IDictionary<string, IDictionary<string, object>>>> GetManagedObjectsAsync();
    Task<IDisposable> WatchInterfacesAddedAsync(Action<(ObjectPath @object, IDictionary<string, IDictionary<string, object>> interfaces)> handler, Action<Exception>? onError = null);
}

[DBusInterface("org.bluez.Adapter1")]
internal interface IAdapter1 : IDBusObject
{
    Task StartDiscoveryAsync();
    Task StopDiscoveryAsync();
    Task<T> GetAsync<T>(string prop);
    Task<Adapter1Properties> GetAllAsync();
    Task<IDisposable> WatchPropertiesAsync(Action<PropertyChanges> handler);
}

[Dictionary]
internal sealed class Adapter1Properties
{
    public string Address { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Alias { get; set; } = string.Empty;
    public bool Powered { get; set; }
}

[DBusInterface("org.bluez.Device1")]
internal interface IDevice1 : IDBusObject
{
    Task ConnectAsync();
    Task DisconnectAsync();
    Task<Device1Properties> GetAllAsync();
    Task<T> GetAsync<T>(string prop);
    Task<IDisposable> WatchPropertiesAsync(Action<PropertyChanges> handler);
}

[Dictionary]
internal sealed class Device1Properties
{
    public string Address { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Alias { get; set; } = string.Empty;
    public short RSSI { get; set; }
    public bool Connected { get; set; }
    public bool ServicesResolved { get; set; }
    public string[] UUIDs { get; set; } = [];
    public IDictionary<ushort, byte[]> ManufacturerData { get; set; } = new Dictionary<ushort, byte[]>();
}

[DBusInterface("org.bluez.GattService1")]
internal interface IGattService1 : IDBusObject
{
    Task<GattServiceProperties> GetAllAsync();
}

[Dictionary]
internal sealed class GattServiceProperties
{
    public string UUID { get; set; } = string.Empty;
}

[DBusInterface("org.bluez.GattCharacteristic1")]
internal interface IGattCharacteristic1 : IDBusObject
{
    Task<byte[]> ReadValueAsync(IDictionary<string, object> options);
    Task WriteValueAsync(byte[] value, IDictionary<string, object> options);
    Task StartNotifyAsync();
    Task StopNotifyAsync();
    Task<GattCharacteristicProperties> GetAllAsync();
    Task<IDisposable> WatchPropertiesAsync(Action<PropertyChanges> handler);
}

[Dictionary]
internal sealed class GattCharacteristicProperties
{
    public string UUID { get; set; } = string.Empty;
    public string[] Flags { get; set; } = [];
    public byte[] Value { get; set; } = [];
}
