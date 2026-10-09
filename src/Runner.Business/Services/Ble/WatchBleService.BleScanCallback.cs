using Android.Bluetooth.LE;
using Runner.Business.Models.WatchBle;

namespace Runner.Business.Services.Ble;

public partial class WatchBleService
{
    private class BleScanCallback : ScanCallback
    {
        private readonly WatchBleService _service;
        public BleScanCallback(WatchBleService service) => _service = service;

        public override void OnScanResult(ScanCallbackType callbackType, ScanResult? result)
        {
            if (result?.Device?.Name?.Contains(WatchConstants.WATCH_NAME_PREFIX, StringComparison.OrdinalIgnoreCase) == true)
            {
                var name = result.Device.Name;
                var addr = result.Device.Address;
                _service.Log($"Found: {name} [{addr}]");
                _service.OnDeviceFound?.Invoke(addr);
                _service._scanCts?.Cancel();
            }
        }

        public override void OnScanFailed(ScanFailure errorCode)
        {
            _service.Log($"Scan failed: {errorCode}");
        }
    }
}
