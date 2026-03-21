namespace BleDemo.Maui
{
	public static class PermissionHelper
	{

		#region Bluetooth Permission
		private static readonly BluetoothPermissions _bluetoothPermissions
#if ANDROID
			//= new(scan: true, connect: true, advertise: false, bluetoothLocation: false);
			= new (scan: true, connect: true, advertise: false, bluetoothLocation: true);
#else
			= new();
#endif

		public static async Task<bool> CheckBluetoothStatus()
		{
			try
			{
				var requestStatus = await _bluetoothPermissions.CheckStatusAsync();
				return requestStatus == PermissionStatus.Granted;
			}
			catch (Exception)
			{
				// logger.LogError(ex);
				return false;
			}
		}

		public static async Task<bool> RequestBluetoothAccess()
		{
			try
			{
				var requestStatus = await _bluetoothPermissions.RequestAsync();
				return requestStatus == PermissionStatus.Granted;
			}
			catch (Exception)
			{
				// logger.LogError(ex);
				return false;
			}
		}
		#endregion

		#region Android NotificationReader

		public static Action? RequestNotificationReaderCheckDelegate { get; internal set; }

		public static Func<bool>? NotificationReaderPermissionCheckDelegate { get; internal set; }

		public static bool CheckNotificationReaderAccess()
		{
			if (NotificationReaderPermissionCheckDelegate == null)
			{
				// not available in this platform
				return true;
			}
			return NotificationReaderPermissionCheckDelegate();
		}

		public static void RequestNotificationReaderAccess()
		{
			RequestNotificationReaderCheckDelegate?.Invoke();
		}
		#endregion
	}
}
