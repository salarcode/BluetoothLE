using CoreBluetooth;

namespace Salar.BluetoothLE.iOS;

/// <summary>
/// Bridges CBCentralManager delegate callbacks into library events.
/// </summary>
public class IosCentralManagerDelegate : CBCentralManagerDelegate
{
    public event Action<CBCentralManager>? StateUpdated;
    public event Action<CBCentralManager, CBPeripheral, NSDictionary, NSNumber>? OnDiscoveredPeripheral;
    public event Action<CBCentralManager, CBPeripheral>? OnConnectedPeripheral;
    public event Action<CBCentralManager, CBPeripheral, NSError?>? OnFailedToConnectPeripheral;
    public event Action<CBCentralManager, CBPeripheral, NSError?>? OnDisconnectedPeripheral;

    /// <summary>
    /// Publishes central manager state updates.
    /// </summary>
    public override void UpdatedState(CBCentralManager central) => StateUpdated?.Invoke(central);

    /// <summary>
    /// Publishes discovered peripheral events.
    /// </summary>
    public override void DiscoveredPeripheral(CBCentralManager central, CBPeripheral peripheral, NSDictionary advertisementData, NSNumber rssi)
        => OnDiscoveredPeripheral?.Invoke(central, peripheral, advertisementData, rssi);

    /// <summary>
    /// Publishes successful peripheral connection events.
    /// </summary>
    public override void ConnectedPeripheral(CBCentralManager central, CBPeripheral peripheral)
        => OnConnectedPeripheral?.Invoke(central, peripheral);

    /// <summary>
    /// Publishes failed peripheral connection events.
    /// </summary>
    public override void FailedToConnectPeripheral(CBCentralManager central, CBPeripheral peripheral, NSError? error)
        => OnFailedToConnectPeripheral?.Invoke(central, peripheral, error);

    /// <summary>
    /// Publishes peripheral disconnection events.
    /// </summary>
    public override void DisconnectedPeripheral(CBCentralManager central, CBPeripheral peripheral, NSError? error)
        => OnDisconnectedPeripheral?.Invoke(central, peripheral, error);
}
