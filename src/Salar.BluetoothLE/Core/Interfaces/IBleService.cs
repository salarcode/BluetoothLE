namespace Salar.BluetoothLE.Core.Interfaces;

/// <summary>
/// Represents a BLE GATT service that exposes characteristics.
/// </summary>
public interface IBleService
{
    Guid Uuid { get; }

    /// <summary>
    /// Gets the characteristics exposed by this service.
    /// </summary>
    Task<IReadOnlyList<IBleCharacteristic>> GetCharacteristicsAsync(CancellationToken cancellationToken = default);
    /// <summary>
    /// Gets the characteristic with the specified UUID.
    /// </summary>
    Task<IBleCharacteristic?> GetCharacteristicAsync(Guid characteristicUuid, CancellationToken cancellationToken = default);
}
