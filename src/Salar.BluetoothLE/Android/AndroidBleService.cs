using Android.Bluetooth;
using Salar.BluetoothLE.Core.Interfaces;

namespace Salar.BluetoothLE.Android;

/// <summary>
/// Implements a BLE GATT service for Android devices.
/// </summary>
public class AndroidBleService : IBleService
{
    private readonly BluetoothGattService _service;
    private readonly AndroidBleDevice _device;
    private List<IBleCharacteristic>? _characteristics;

    /// <summary>
    /// Initializes a new AndroidBleService instance.
    /// </summary>
    public AndroidBleService(BluetoothGattService service, AndroidBleDevice device)
    {
        _service = service;
        _device = device;
    }

    public Guid Uuid => Guid.Parse(_service.Uuid!.ToString()!);

    /// <summary>
    /// Gets the characteristics exposed by this service.
    /// </summary>
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

    /// <summary>
    /// Gets the characteristic with the specified UUID.
    /// </summary>
    public async Task<IBleCharacteristic?> GetCharacteristicAsync(Guid characteristicUuid, CancellationToken cancellationToken = default)
    {
        var chars = await GetCharacteristicsAsync(cancellationToken);
        return chars.FirstOrDefault(c => c.Uuid == characteristicUuid);
    }
}
