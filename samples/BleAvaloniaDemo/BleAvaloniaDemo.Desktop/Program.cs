using Avalonia;
using System;
using System.Threading.Tasks;
using Salar.BluetoothLE;
#if WINDOWS
using Salar.BluetoothLE.Windows;
#endif

namespace BleAvaloniaDemo.Desktop
{
    internal sealed class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args)
        {
            BleDemoPlatformServices.Initialize(CreateAdapter, static () => Task.FromResult(true));
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();

        private static IBleAdapter CreateAdapter() =>
#if WINDOWS
            new WindowsBleAdapter();
#else
            OperatingSystem.IsLinux()
                ? new Salar.BluetoothLE.Linux.LinuxBleAdapter()
                : throw new PlatformNotSupportedException("BleAvaloniaDemo requires a supported BLE platform.");
#endif
    }
}
