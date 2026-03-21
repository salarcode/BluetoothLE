using CoreBluetooth;

namespace Salar.BluetoothLE.iOS;

public class IosCentralManagerDelegate : CBCentralManagerDelegate
{
    public event Action<CBCentralManager>? StateUpdated;
    public event Action<CBCentralManager, CBPeripheral, NSDictionary, NSNumber>? OnDiscoveredPeripheral;
    public event Action<CBCentralManager, CBPeripheral>? OnConnectedPeripheral;
    public event Action<CBCentralManager, CBPeripheral, NSError?>? OnFailedToConnectPeripheral;
    public event Action<CBCentralManager, CBPeripheral, NSError?>? OnDisconnectedPeripheral;

    public override void UpdatedState(CBCentralManager central) => StateUpdated?.Invoke(central);

    public override void DiscoveredPeripheral(CBCentralManager central, CBPeripheral peripheral, NSDictionary advertisementData, NSNumber rssi)
        => OnDiscoveredPeripheral?.Invoke(central, peripheral, advertisementData, rssi);

    public override void ConnectedPeripheral(CBCentralManager central, CBPeripheral peripheral)
        => OnConnectedPeripheral?.Invoke(central, peripheral);

    public override void FailedToConnectPeripheral(CBCentralManager central, CBPeripheral peripheral, NSError? error)
        => OnFailedToConnectPeripheral?.Invoke(central, peripheral, error);

    public override void DisconnectedPeripheral(CBCentralManager central, CBPeripheral peripheral, NSError? error)
        => OnDisconnectedPeripheral?.Invoke(central, peripheral, error);
}