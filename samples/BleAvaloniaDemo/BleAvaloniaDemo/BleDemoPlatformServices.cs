using System;
using System.Threading.Tasks;
using Salar.BluetoothLE;

namespace BleAvaloniaDemo;

internal static class BleDemoPlatformServices
{
    public static Func<IBleAdapter>? CreateAdapter { get; private set; }

    public static Func<Task<bool>> RequestBluetoothAccessAsync { get; private set; } = static () => Task.FromResult(true);

    public static void Initialize(Func<IBleAdapter> createAdapter, Func<Task<bool>>? requestBluetoothAccessAsync = null)
    {
        CreateAdapter = createAdapter;
        RequestBluetoothAccessAsync = requestBluetoothAccessAsync ?? (static () => Task.FromResult(true));
    }
}
