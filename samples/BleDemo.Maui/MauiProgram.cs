using Salar.BluetoothLE;

namespace BleDemo.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddBluetoothLE();
        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddSingleton<MainPage>();

        return builder.Build();
    }
}
