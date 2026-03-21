using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Salar.BluetoothLE.Core.Interfaces;
using Salar.BluetoothLE.Core.Models;

namespace Salar.BluetoothLE.Windows;

public class WindowsBleService : IBleService, IDisposable
{
    private readonly GattDeviceService _service;
    private List<IBleCharacteristic>? _characteristics;

    public WindowsBleService(GattDeviceService service)
    {
        _service = service;
    }

    public Guid Uuid => _service.Uuid;

    public async Task<IReadOnlyList<IBleCharacteristic>> GetCharacteristicsAsync(CancellationToken cancellationToken = default)
    {
        if (_characteristics == null)
        {
            var result = await _service.GetCharacteristicsAsync();
            if (result.Status != GattCommunicationStatus.Success)
                throw new BleException(BleErrorCode.OperationFailed, $"Failed to get characteristics: {result.Status}");
            _characteristics = result.Characteristics
                .Select(c => (IBleCharacteristic)new WindowsBleCharacteristic(c))
                .ToList();
        }
        return _characteristics.AsReadOnly();
    }

    public async Task<IBleCharacteristic?> GetCharacteristicAsync(Guid characteristicUuid, CancellationToken cancellationToken = default)
    {
        var chars = await GetCharacteristicsAsync(cancellationToken);
        return chars.FirstOrDefault(c => c.Uuid == characteristicUuid);
    }

    public void Dispose()
    {
        // GattDeviceService is a WinRT COM object.  Disposing it releases the
        // GATT session reference held by the service.  Failure to dispose every
        // GattDeviceService prevents Windows from fully releasing the BLE device
        // after BluetoothLEDevice.Dispose(), which in turn suppresses the
        // device's advertisements from subsequent BluetoothLEAdvertisementWatcher
        // scans.
        _service.Dispose();
    }
}
