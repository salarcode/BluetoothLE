using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Android.App;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.Content;

namespace BleAvaloniaDemo.Android;

internal static class AndroidPermissionService
{
    // Arbitrary app-local request code used to correlate the Bluetooth permission callback.
    private const int BluetoothPermissionRequestCode = 4107;
    private static TaskCompletionSource<bool>? _pendingRequest;

    public static Task<bool> RequestBluetoothAccessAsync(Activity activity)
    {
        var requiredPermissions = GetRequiredPermissionsLimited()
            .Where(permission => ContextCompat.CheckSelfPermission(activity, permission) != Permission.Granted)
            .Distinct()
            .ToArray();

        if (requiredPermissions.Length == 0)
        {
            return Task.FromResult(true);
        }

        if (_pendingRequest is not null)
        {
            return _pendingRequest.Task;
        }

        _pendingRequest = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        activity.RequestPermissions(requiredPermissions, BluetoothPermissionRequestCode);
        return _pendingRequest.Task;
    }

    public static void CompleteRequest(int requestCode, string[]? permissions, Permission[]? grantResults)
    {
        if (requestCode != BluetoothPermissionRequestCode || _pendingRequest is null)
        {
            return;
        }

        var pendingRequest = _pendingRequest;
        _pendingRequest = null;

        var granted = grantResults is { Length: > 0 } && grantResults.All(result => result == Permission.Granted);
        pendingRequest.TrySetResult(granted);
    }

    private static IEnumerable<string> GetRequiredPermissionsLimited()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.S)
        {
            yield return global::Android.Manifest.Permission.BluetoothScan;
            yield return global::Android.Manifest.Permission.BluetoothConnect;
            yield return global::Android.Manifest.Permission.AccessFineLocation;
            yield break;
        }

        yield return global::Android.Manifest.Permission.Bluetooth;
        yield return global::Android.Manifest.Permission.BluetoothAdmin;
        yield return Build.VERSION.SdkInt >= BuildVersionCodes.Q
            ? global::Android.Manifest.Permission.AccessFineLocation
            : global::Android.Manifest.Permission.AccessCoarseLocation;
    }
}
