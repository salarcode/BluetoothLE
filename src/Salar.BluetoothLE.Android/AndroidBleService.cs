using Android.Bluetooth;
using Salar.BluetoothLE.Core.Interfaces;

namespace Salar.BluetoothLE.Android;

public class AndroidBleService : IBleService
{
    private readonly BluetoothGattService _service;
    private readonly AndroidBleDevice _device;
    private List<IBleCharacteristic>? _characteristics;

    public AndroidBleService(BluetoothGattService service, AndroidBleDevice device)
    {
        _service = service;
        _device = device;
    }

    public Guid Uuid => Guid.Parse(_service.Uuid!.ToString()!);

    public Task<IReadOnlyList<IBleCharacteristic>> GetCharacteristicsAsync(CancellationToken cancellationToken = default)
    {
        if (_characteristics == null)
        {
            _characteristics = _service.Characteristics!
                .Select(c => (IBleCharacteristic)new AndroidBleCharacteristic(c, _device))
                .ToList();
        }
        return Task.FromResult<IReadOnlyList<IBleCharacteristic>>(_characteristics.AsReadOnly());
    }

    public async Task<IBleCharacteristic?> GetCharacteristicAsync(Guid characteristicUuid, CancellationToken cancellationToken = default)
    {
        var chars = await GetCharacteristicsAsync(cancellationToken);
        return chars.FirstOrDefault(c => c.Uuid == characteristicUuid);
    }
}
