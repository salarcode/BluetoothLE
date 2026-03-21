using Android.Bluetooth.LE;
using Salar.BluetoothLE.Core.Models;
using ScanResult = Android.Bluetooth.LE.ScanResult;

namespace Salar.BluetoothLE.Android;

public class AndroidScanCallback : ScanCallback
{
	private readonly Action<Core.Models.ScanResult> _onResult;
	private readonly Action<int>? _onFailed;

	public AndroidScanCallback(Action<Core.Models.ScanResult> onResult, Action<int>? onFailed = null)
	{
		_onResult = onResult;
		_onFailed = onFailed;
	}

	public override void OnScanResult(ScanCallbackType callbackType, ScanResult? result)
	{
		PublishResult(result);
	}

	public override void OnBatchScanResults(IList<ScanResult>? results)
	{
		if (results == null) return;

		foreach (var result in results)
			PublishResult(result);
	}

	public override void OnScanFailed(ScanFailure errorCode)
	{
		_onFailed?.Invoke((int)errorCode);
	}

	private void PublishResult(ScanResult? result)
	{
		if (result?.Device == null) return;

		try
		{
			var serviceUuids = result.ScanRecord?.ServiceUuids?
				.Select(p => Guid.TryParse(p.Uuid?.ToString(), out var guid) ? guid : Guid.Empty)
				.Where(g => g != Guid.Empty)
				.ToList() ?? [];

			var manufacturerData = new Dictionary<ushort, byte[]>();
			var mfData = result.ScanRecord?.ManufacturerSpecificData;
			if (mfData != null)
			{
				for (int i = 0; i < mfData.Size(); i++)
				{
					var key = (ushort)mfData.KeyAt(i);
					var value = (byte[]?)mfData.ValueAt(i);
					if (value != null)
						manufacturerData[key] = value;
				}
			}

			var scanResult = new Core.Models.ScanResult
			{
				Name = result.Device.Name,
				Address = result.Device.Address ?? string.Empty,
				Rssi = result.Rssi,
				IsConnectable = result.IsConnectable,
				ServiceUuids = serviceUuids,
				ManufacturerData = manufacturerData,
				AdvertisementData = result.ScanRecord?.GetBytes()
			};

			_onResult(scanResult);
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error while publishing Bluetooth LE scan result: {ex}");
#if DEBUG
			throw;
#endif
		}
	}
}
