using global::Linux.Bluetooth;
using global::Linux.Bluetooth.Extensions;
using Salar.BluetoothLE.Core.Interfaces;

namespace Salar.BluetoothLE.Linux;

/// <summary>
/// Implements a BLE GATT service for Linux devices.
/// </summary>
public sealed class LinuxBleService : IBleService, IDisposable
{
    private readonly IGattService1 _service;
    private List<IBleCharacteristic>? _characteristics;

    /// <summary>
    /// Initializes a new LinuxBleService instance.
    /// </summary>
    public LinuxBleService(IGattService1 service, Guid uuid)
    {
        _service = service;
        Uuid = uuid;
    }

    public Guid Uuid { get; }

    /// <summary>
    /// Gets the characteristics exposed by this service.
    /// </summary>
    public async Task<IReadOnlyList<IBleCharacteristic>> GetCharacteristicsAsync(CancellationToken cancellationToken = default)
    {
        if (_characteristics != null)
            return _characteristics.AsReadOnly();

        var characteristics = await _service.GetCharacteristicsAsync().WaitAsync(cancellationToken);
        var wrapped = new List<IBleCharacteristic>(characteristics.Count);

        foreach (var characteristic in characteristics)
        {
            var properties = await characteristic.GetAllAsync().WaitAsync(cancellationToken);
            var resolved = await _service.GetCharacteristicAsync(properties.UUID).WaitAsync(cancellationToken);
            if (resolved == null)
                continue;

            wrapped.Add(new LinuxBleCharacteristic(
                resolved,
                Guid.Parse(BlueZManager.NormalizeUUID(properties.UUID)),
                properties.Flags ?? []));
        }

        _characteristics = wrapped;
        return _characteristics.AsReadOnly();
    }

    /// <summary>
    /// Gets the characteristic with the specified UUID.
    /// </summary>
    public async Task<IBleCharacteristic?> GetCharacteristicAsync(Guid characteristicUuid, CancellationToken cancellationToken = default)
    {
        var characteristic = await _service.GetCharacteristicAsync(
            BlueZManager.NormalizeUUID(characteristicUuid.ToString())).WaitAsync(cancellationToken);

        if (characteristic == null)
            return null;

        var properties = await characteristic.GetAllAsync().WaitAsync(cancellationToken);
        return new LinuxBleCharacteristic(characteristic, characteristicUuid, properties.Flags ?? []);
    }

    /// <summary>
    /// Releases the underlying Linux Bluetooth service resources.
    /// </summary>
    public void Dispose()
    {
        if (_characteristics == null)
            return;

        foreach (var characteristic in _characteristics)
            (characteristic as IDisposable)?.Dispose();

        _characteristics = null;
    }
}
