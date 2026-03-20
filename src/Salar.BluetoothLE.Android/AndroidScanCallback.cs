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
        if (result?.Device == null) return;

        var serviceUuids = result.ScanRecord?.ServiceUuids?
            .Select(p => Guid.Parse(p.Uuid!.ToString()!))
            .ToList() ?? new List<Guid>();

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

    public override void OnScanFailed(ScanFailure errorCode)
    {
        _onFailed?.Invoke((int)errorCode);
    }
}
