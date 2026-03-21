using Android.Bluetooth;
using Salar.BluetoothLE.Core.Models;

namespace Salar.BluetoothLE.Android;

/// <summary>
/// Bridges Android GATT callback events into library-friendly events.
/// </summary>
public class AndroidGattCallback : BluetoothGattCallback
{
    public event Action<BluetoothGatt, ProfileState>? ConnectionStateChanged;
    public event Action<BluetoothGatt, GattStatus>? ServicesDiscovered;
    public event Action<BluetoothGatt, BluetoothGattCharacteristic, GattStatus>? CharacteristicRead;
    public event Action<BluetoothGatt, BluetoothGattCharacteristic, GattStatus>? CharacteristicWritten;
    public event Action<BluetoothGatt, BluetoothGattCharacteristic>? CharacteristicChanged;
    public event Action<BluetoothGatt, GattStatus>? DescriptorWritten;
    public event Action<BluetoothGatt, int, GattStatus>? MtuChanged;

    /// <summary>
    /// Publishes Android connection state change events.
    /// </summary>
    public override void OnConnectionStateChange(BluetoothGatt? gatt, GattStatus status, ProfileState newState)
    {
        if (gatt != null)
            ConnectionStateChanged?.Invoke(gatt, newState);
    }

    /// <summary>
    /// Publishes Android service discovery events.
    /// </summary>
    public override void OnServicesDiscovered(BluetoothGatt? gatt, GattStatus status)
    {
        if (gatt != null)
            ServicesDiscovered?.Invoke(gatt, status);
    }

    /// <summary>
    /// Publishes Android characteristic read events.
    /// </summary>
    public override void OnCharacteristicRead(BluetoothGatt? gatt, BluetoothGattCharacteristic? characteristic, GattStatus status)
    {
        if (gatt != null && characteristic != null)
            CharacteristicRead?.Invoke(gatt, characteristic, status);
    }

    /// <summary>
    /// Publishes Android characteristic write events.
    /// </summary>
    public override void OnCharacteristicWrite(BluetoothGatt? gatt, BluetoothGattCharacteristic? characteristic, GattStatus status)
    {
        if (gatt != null && characteristic != null)
            CharacteristicWritten?.Invoke(gatt, characteristic, status);
    }

    /// <summary>
    /// Publishes Android characteristic change notifications.
    /// </summary>
    public override void OnCharacteristicChanged(BluetoothGatt? gatt, BluetoothGattCharacteristic? characteristic)
    {
        if (gatt != null && characteristic != null)
            CharacteristicChanged?.Invoke(gatt, characteristic);
    }

    /// <summary>
    /// Publishes Android descriptor write events.
    /// </summary>
    public override void OnDescriptorWrite(BluetoothGatt? gatt, BluetoothGattDescriptor? descriptor, GattStatus status)
    {
        if (gatt != null)
            DescriptorWritten?.Invoke(gatt, status);
    }

    /// <summary>
    /// Publishes Android MTU change events.
    /// </summary>
    public override void OnMtuChanged(BluetoothGatt? gatt, int mtu, GattStatus status)
    {
        if (gatt != null)
            MtuChanged?.Invoke(gatt, mtu, status);
    }
}
