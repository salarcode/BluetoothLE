using Salar.BluetoothLE.Core.Enums;
using Salar.BluetoothLE.Core.Interfaces;
using Microsoft.Maui.ApplicationModel;
#if ANDROID
using Android;
#endif

namespace BleDemo.Maui;

internal static class BlePermissionHelper
{
    public static async Task<BleAccessResult> RequestAccessAsync(IBleAdapter adapter)
    {
#if ANDROID
        if (OperatingSystem.IsAndroidVersionAtLeast(31))
        {
            var scan = await Permissions.RequestAsync<BluetoothScanPermission>();
            var connect = await Permissions.RequestAsync<BluetoothConnectPermission>();

            if (scan != PermissionStatus.Granted || connect != PermissionStatus.Granted)
            {
                return new BleAccessResult(false, "Denied", "Bluetooth scan and connect permissions are required.");
            }
        }
        else
        {
            var location = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            if (location != PermissionStatus.Granted)
            {
                return new BleAccessResult(false, "Denied", "Location permission is required for BLE scanning on Android 11 and lower.");
            }
        }
#endif

        var access = await adapter.RequestAccessAsync();
        return access == BlePermissionStatus.Granted
            ? new BleAccessResult(true, access.ToString(), "BLE access granted.")
            : new BleAccessResult(false, access.ToString(), $"BLE access is {access}.");
    }

#if ANDROID
    private sealed class BluetoothScanPermission : Permissions.BasePlatformPermission
    {
        public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
        [
            (Manifest.Permission.BluetoothScan, true)
        ];
    }

    private sealed class BluetoothConnectPermission : Permissions.BasePlatformPermission
    {
        public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
        [
            (Manifest.Permission.BluetoothConnect, true)
        ];
    }
#endif
}

internal sealed record BleAccessResult(bool IsGranted, string DisplayStatus, string Message);
