using CoreBluetooth;
using Salar.BluetoothLE.Core.Interfaces;

namespace Salar.BluetoothLE.iOS;

/// <summary>
/// Implements a BLE GATT service for iOS peripherals.
/// </summary>
public class IosBleService : IBleService
{
    private readonly CBService _service;
    private readonly IosBleDevice _device;
    private List<IBleCharacteristic>? _characteristics;

    /// <summary>
    /// Initializes a new IosBleService instance.
    /// </summary>
    public IosBleService(CBService service, IosBleDevice device)
    {
        _service = service;
        _device = device;
    }

    public Guid Uuid => Guid.Parse(_service.UUID.ToString());

    /// <summary>
    /// Gets the characteristics exposed by this service.
    /// </summary>
    public async Task<IReadOnlyList<IBleCharacteristic>> GetCharacteristicsAsync(CancellationToken cancellationToken = default)
    {
        if (_characteristics == null)
        {
            await _device.DiscoverCharacteristicsAsync(_service, cancellationToken);
            _characteristics = _service.Characteristics?
                .Select(c => (IBleCharacteristic)new IosBleCharacteristic(c, _device))
                .ToList() ?? new List<IBleCharacteristic>();
        }
        return _characteristics.AsReadOnly();
    }

    /// <summary>
    /// Gets the characteristic with the specified UUID.
    /// </summary>
    public async Task<IBleCharacteristic?> GetCharacteristicAsync(Guid characteristicUuid, CancellationToken cancellationToken = default)
    {
        var chars = await GetCharacteristicsAsync(cancellationToken);
        return chars.FirstOrDefault(c => c.Uuid == characteristicUuid);
    }
}
