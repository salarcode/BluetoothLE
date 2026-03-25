using Android.App;

// Note: copy this file to `/Platforms/Android/AndroidPermissions.cs` in your app.

[assembly: UsesPermission(Android.Manifest.Permission.AccessCoarseLocation, MaxSdkVersion = 30)]
[assembly: UsesPermission(Android.Manifest.Permission.AccessFineLocation, MaxSdkVersion = 30)]

[assembly: UsesPermission(Android.Manifest.Permission.Bluetooth, MaxSdkVersion = 30)]
[assembly: UsesPermission(Android.Manifest.Permission.BluetoothAdmin, MaxSdkVersion = 30)]
[assembly: UsesPermission(Android.Manifest.Permission.BluetoothScan)]
[assembly: UsesPermission(Android.Manifest.Permission.BluetoothConnect)]
