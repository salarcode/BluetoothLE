using CoreBluetooth;
using Salar.BluetoothLE.Core.Interfaces;

namespace Salar.BluetoothLE.iOS;

public class IosBleService : IBleService
{
    private readonly CBService _service;
    private readonly IosBleDevice _device;
    private List<IBleCharacteristic>? _characteristics;

    public IosBleService(CBService service, IosBleDevice device)
    {
        _service = service;
        _device = device;
    }

    public Guid Uuid => Guid.Parse(_service.UUID.ToString());

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

    public async Task<IBleCharacteristic?> GetCharacteristicAsync(Guid characteristicUuid, CancellationToken cancellationToken = default)
    {
        var chars = await GetCharacteristicsAsync(cancellationToken);
        return chars.FirstOrDefault(c => c.Uuid == characteristicUuid);
    }
}
