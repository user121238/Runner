using Android.Bluetooth;
using Android.Content;
using Microsoft.Extensions.Logging;
using Runner.Business.Models.WatchBle;

namespace Runner.Business.Services.Ble;

public partial class WatchBleService : IWatchBleService
{
    private const string TAG = "[WatchBleService]";

    private readonly ILogger<WatchBleService> _logger;
    private BluetoothAdapter? _bluetoothAdapter;
    private CancellationTokenSource? _scanCts;

    public event Action<string>? OnLog;
    public event Action<bool>? OnConnectionChanged;
    public event Action<WatchSportInfo>? OnSportInfoReceived;
    public event Action<string>? OnDeviceFound;

    public WatchBleService(ILogger<WatchBleService> logger)
    {
        _logger = logger;
    }

    public async Task StartScanAsync(int timeoutSeconds = 10)
    {
        var context = Android.App.Application.Context;
        var bluetoothManager = (BluetoothManager?)context.GetSystemService(Context.BluetoothService);
        _bluetoothAdapter = bluetoothManager?.Adapter;

        if (_bluetoothAdapter == null) { Log("Bluetooth not supported"); return; }
        if (!_bluetoothAdapter.IsEnabled) { Log("Bluetooth is disabled"); return; }

        // Check already bonded devices first
        foreach (var device in _bluetoothAdapter.BondedDevices ?? Array.Empty<BluetoothDevice>())
        {
            if (TryReportDevice(device, "Found bonded device"))
                return;
        }

        var scanner = _bluetoothAdapter.BluetoothLeScanner;
        if (scanner == null) { Log("BLE scanner not available"); return; }

        _scanCts = new CancellationTokenSource();
        var scanCallback = new BleScanCallback(this);
        scanner.StartScan(scanCallback);

        Log($"Scanning for {WatchConstants.WATCH_NAME_PREFIX}... (timeout: {timeoutSeconds}s)");

        try
        {
            await Task.Delay(timeoutSeconds * 1000, _scanCts.Token);
        }
        catch (TaskCanceledException) { }

        scanner.StopScan(scanCallback);
        Log("Scan stopped");
    }

    private bool TryReportDevice(BluetoothDevice? device, string description)
    {
        var name = device?.Name;
        if (name?.Contains(WatchConstants.WATCH_NAME_PREFIX, StringComparison.OrdinalIgnoreCase) != true)
            return false;

        var address = device?.Address;
        if (string.IsNullOrWhiteSpace(address))
        {
            Log($"Ignoring device without an address: {name}");
            return false;
        }

        Log($"{description}: {name} [{address}]");
        OnDeviceFound?.Invoke(address);
        return true;
    }

    private void Log(string message)
    {
        _logger.LogInformation("{Tag} {Message}", TAG, message);
        System.Diagnostics.Debug.WriteLine($"{TAG} {message}");
        OnLog?.Invoke(message);
    }
}
