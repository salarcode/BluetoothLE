using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Salar.BluetoothLE.Core.Interfaces;
using Salar.BluetoothLE.Core.Models;

namespace Salar.BluetoothLE.Windows;

/// <summary>
/// Implements a BLE GATT service for Windows devices.
/// </summary>
public class WindowsBleService : IBleService, IDisposable
{
    private readonly GattDeviceService _service;
    private List<IBleCharacteristic>? _characteristics;

    /// <summary>
    /// Initializes a new WindowsBleService instance.
    /// </summary>
    public WindowsBleService(GattDeviceService service)
    {
        _service = service;
    }

    public Guid Uuid => _service.Uuid;

    /// <summary>
    /// Gets the characteristics exposed by this service.
    /// </summary>
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

    /// <summary>
    /// Gets the characteristic with the specified UUID.
    /// </summary>
    public async Task<IBleCharacteristic?> GetCharacteristicAsync(Guid characteristicUuid, CancellationToken cancellationToken = default)
    {
        var chars = await GetCharacteristicsAsync(cancellationToken);
        return chars.FirstOrDefault(c => c.Uuid == characteristicUuid);
    }

    /// <summary>
    /// Releases the underlying Windows GATT service resources.
    /// </summary>
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
