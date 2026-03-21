using Android.App;

// Note: this file needs to be compied to `/Platforms/Android/AndroidPermissions.cs` of you app

[assembly: UsesPermission(Android.Manifest.Permission.AccessCoarseLocation)]
[assembly: UsesPermission(Android.Manifest.Permission.AccessFineLocation)]

[assembly: UsesPermission(Android.Manifest.Permission.Bluetooth, MaxSdkVersion = 30)]
[assembly: UsesPermission(Android.Manifest.Permission.BluetoothAdmin, MaxSdkVersion = 30)]
[assembly: UsesPermission(Android.Manifest.Permission.BluetoothScan)]
[assembly: UsesPermission(Android.Manifest.Permission.BluetoothConnect)]
[assembly: UsesPermission(Android.Manifest.Permission.BluetoothPrivileged)]
