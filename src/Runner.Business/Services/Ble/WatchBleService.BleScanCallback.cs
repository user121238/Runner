using Android.Bluetooth.LE;

namespace Runner.Business.Services.Ble;

public partial class WatchBleService
{
    private class BleScanCallback : ScanCallback
    {
        private readonly WatchBleService _service;
        public BleScanCallback(WatchBleService service) => _service = service;

        public override void OnScanResult(ScanCallbackType callbackType, ScanResult? result)
        {
            if (_service.TryReportDevice(result?.Device, "Found"))
                _service._scanCts?.Cancel();
        }

        public override void OnScanFailed(ScanFailure errorCode)
        {
            _service.Log($"Scan failed: {errorCode}");
        }
    }
}
