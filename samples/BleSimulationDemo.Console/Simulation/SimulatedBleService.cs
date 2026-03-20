using Salar.BluetoothLE.Core.Interfaces;

namespace BleDemo.Console.Simulation;

internal sealed class SimulatedBleService : IBleService
{
    private readonly string _name;

    public Guid Uuid { get; }

    public SimulatedBleService(Guid uuid, string name)
    {
        Uuid  = uuid;
        _name = name;
    }

    public Task<IReadOnlyList<IBleCharacteristic>> GetCharacteristicsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<IBleCharacteristic> chars = new List<IBleCharacteristic>
        {
            new SimulatedBleCharacteristic(Guid.Parse("00002A37-0000-1000-8000-00805F9B34FB"), "Heart Rate Measurement", canRead: false, canNotify: true),
            new SimulatedBleCharacteristic(Guid.Parse("00002A38-0000-1000-8000-00805F9B34FB"), "Body Sensor Location",   canRead: true,  canNotify: false),
            new SimulatedBleCharacteristic(Guid.Parse("00002A39-0000-1000-8000-00805F9B34FB"), "Heart Rate Control",     canRead: false, canWrite: true),
        };
        return Task.FromResult(chars);
    }

    public Task<IBleCharacteristic?> GetCharacteristicAsync(Guid characteristicUuid, CancellationToken cancellationToken = default)
    {
        IBleCharacteristic? ch = new SimulatedBleCharacteristic(characteristicUuid, "Simulated Characteristic", canRead: true, canWrite: true, canNotify: true);
        return Task.FromResult<IBleCharacteristic?>(ch);
    }
}
