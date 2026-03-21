using CoreBluetooth;
using Foundation;

namespace Salar.BluetoothLE.iOS;

public class IosPeripheralDelegate : CBPeripheralDelegate
{
    public event Action<CBPeripheral, NSError?>? OnDiscoveredServices;
    public event Action<CBPeripheral, CBService, NSError?>? OnDiscoveredCharacteristics;
    public event Action<CBPeripheral, CBCharacteristic, NSError?>? OnUpdatedCharacteristicValue;
    public event Action<CBPeripheral, CBCharacteristic, NSError?>? OnWroteCharacteristicValue;
    public event Action<CBPeripheral, CBDescriptor, NSError?>? OnWroteDescriptorValue;
    public event Action<CBPeripheral, int>? UpdatedMtu;

    public override void DiscoveredService(CBPeripheral peripheral, NSError? error)
        => OnDiscoveredServices?.Invoke(peripheral, error);

    public override void DiscoveredCharacteristics(CBPeripheral peripheral, CBService service, NSError? error)
        => OnDiscoveredCharacteristics?.Invoke(peripheral, service, error);

    public override void UpdatedCharacterteristicValue(CBPeripheral peripheral, CBCharacteristic characteristic, NSError? error)
        => OnUpdatedCharacteristicValue?.Invoke(peripheral, characteristic, error);

    public override void WroteCharacteristicValue(CBPeripheral peripheral, CBCharacteristic characteristic, NSError? error)
        => OnWroteCharacteristicValue?.Invoke(peripheral, characteristic, error);

    public override void WroteDescriptorValue(CBPeripheral peripheral, CBDescriptor descriptor, NSError? error)
        => OnWroteDescriptorValue?.Invoke(peripheral, descriptor, error);
}
