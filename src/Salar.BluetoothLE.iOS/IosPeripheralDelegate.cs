using CoreBluetooth;
using Foundation;

namespace Salar.BluetoothLE.iOS;

public class IosPeripheralDelegate : CBPeripheralDelegate
{
    public event Action<CBPeripheral, NSError?>? DiscoveredServices;
    public event Action<CBPeripheral, CBService, NSError?>? DiscoveredCharacteristics;
    public event Action<CBPeripheral, CBCharacteristic, NSError?>? UpdatedCharacteristicValue;
    public event Action<CBPeripheral, CBCharacteristic, NSError?>? WroteCharacteristicValue;
    public event Action<CBPeripheral, CBDescriptor, NSError?>? WroteDescriptorValue;
    public event Action<CBPeripheral, int>? UpdatedMtu;

    public override void DiscoveredService(CBPeripheral peripheral, NSError? error)
        => DiscoveredServices?.Invoke(peripheral, error);

    public override void DiscoveredCharacteristic(CBPeripheral peripheral, CBService service, NSError? error)
        => DiscoveredCharacteristics?.Invoke(peripheral, service, error);

    public override void UpdatedCharacteriteValue(CBPeripheral peripheral, CBCharacteristic characteristic, NSError? error)
        => UpdatedCharacteristicValue?.Invoke(peripheral, characteristic, error);

    public override void WroteCharacteristicValue(CBPeripheral peripheral, CBCharacteristic characteristic, NSError? error)
        => WroteCharacteristicValue?.Invoke(peripheral, characteristic, error);

    public override void WroteDescriptorValue(CBPeripheral peripheral, CBDescriptor descriptor, NSError? error)
        => WroteDescriptorValue?.Invoke(peripheral, descriptor, error);
}
