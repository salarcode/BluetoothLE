namespace Salar.BluetoothLE.Core.Interfaces;

public interface IBleService
{
    Guid Uuid { get; }

    Task<IReadOnlyList<IBleCharacteristic>> GetCharacteristicsAsync(CancellationToken cancellationToken = default);
    Task<IBleCharacteristic?> GetCharacteristicAsync(Guid characteristicUuid, CancellationToken cancellationToken = default);
}
