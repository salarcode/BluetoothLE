using Android.Bluetooth;
using Salar.BluetoothLE.Core.Models;

namespace Salar.BluetoothLE.Android;

public class AndroidGattCallback : BluetoothGattCallback
{
    public event Action<BluetoothGatt, ProfileState>? ConnectionStateChanged;
    public event Action<BluetoothGatt, GattStatus>? ServicesDiscovered;
    public event Action<BluetoothGatt, BluetoothGattCharacteristic, GattStatus>? CharacteristicRead;
    public event Action<BluetoothGatt, BluetoothGattCharacteristic, GattStatus>? CharacteristicWritten;
    public event Action<BluetoothGatt, BluetoothGattCharacteristic>? CharacteristicChanged;
    public event Action<BluetoothGatt, GattStatus>? DescriptorWritten;
    public event Action<BluetoothGatt, int, GattStatus>? MtuChanged;

    public override void OnConnectionStateChange(BluetoothGatt? gatt, GattStatus status, ProfileState newState)
    {
        if (gatt != null)
            ConnectionStateChanged?.Invoke(gatt, newState);
    }

    public override void OnServicesDiscovered(BluetoothGatt? gatt, GattStatus status)
    {
        if (gatt != null)
            ServicesDiscovered?.Invoke(gatt, status);
    }

    public override void OnCharacteristicRead(BluetoothGatt? gatt, BluetoothGattCharacteristic? characteristic, GattStatus status)
    {
        if (gatt != null && characteristic != null)
            CharacteristicRead?.Invoke(gatt, characteristic, status);
    }

    public override void OnCharacteristicWrite(BluetoothGatt? gatt, BluetoothGattCharacteristic? characteristic, GattStatus status)
    {
        if (gatt != null && characteristic != null)
            CharacteristicWritten?.Invoke(gatt, characteristic, status);
    }

    public override void OnCharacteristicChanged(BluetoothGatt? gatt, BluetoothGattCharacteristic? characteristic)
    {
        if (gatt != null && characteristic != null)
            CharacteristicChanged?.Invoke(gatt, characteristic);
    }

    public override void OnDescriptorWrite(BluetoothGatt? gatt, BluetoothGattDescriptor? descriptor, GattStatus status)
    {
        if (gatt != null)
            DescriptorWritten?.Invoke(gatt, status);
    }

    public override void OnMtuChanged(BluetoothGatt? gatt, int mtu, GattStatus status)
    {
        if (gatt != null)
            MtuChanged?.Invoke(gatt, mtu, status);
    }
}
