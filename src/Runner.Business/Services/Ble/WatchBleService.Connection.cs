using Android.Bluetooth;

namespace Runner.Business.Services.Ble;

public partial class WatchBleService
{
    private BluetoothGatt? _gatt;
    private BleGattCallback? _gattCallback;
    private TaskCompletionSource<bool>? _connectionTcs;
    private TaskCompletionSource<bool>? _serviceDiscoveredTcs;

    public bool IsConnected => _gatt?.Services?.Count > 0;

    public async Task ConnectAsync(string deviceAddress)
    {
        if (_bluetoothAdapter == null) { Log("Bluetooth not initialized"); return; }

        var device = _bluetoothAdapter.GetRemoteDevice(deviceAddress);
        if (device == null) { Log($"Device not found: {deviceAddress}"); return; }

        var context = Android.App.Application.Context;

        // Clean up any existing GATT connection first
        if (_gatt != null)
        {
            _gatt.Disconnect();
            _gatt.Close();
            _gatt = null;
            _gattCallback?.Dispose();
            _gattCallback = null;
            await Task.Delay(500);
        }

        _connectionTcs = new TaskCompletionSource<bool>();

        // Try LE transport first (needed for FDAA/MiConnect service)
        Log($"Connecting to {device.Name} ({deviceAddress}) via LE...");
        _gattCallback = new BleGattCallback(this);
        _gatt = device.ConnectGatt(context, false, _gattCallback, BluetoothTransports.Le);

        var connected = await AwaitConnectionAsync(8000);

        // If LE transport failed, fall back to auto
        if (!connected)
        {
            Log("LE transport failed, retrying with auto transport...");
            if (_gatt != null) { _gatt.Disconnect(); _gatt.Close(); _gatt = null; }
            _gattCallback?.Dispose();

            _connectionTcs = new TaskCompletionSource<bool>();
            _gattCallback = new BleGattCallback(this);
            _gatt = device.ConnectGatt(context, false, _gattCallback);
            connected = await AwaitConnectionAsync(5000);
        }

        if (!connected)
        {
            Log($"Failed to connect to {deviceAddress}");
            _gatt = null;
        }
        else
        {
            Log($"Connected: {device.Name}");
        }
    }

    public Task DisconnectAsync()
    {
        if (_gatt != null)
        {
            _gatt.Disconnect();
            _gatt.Close();
        }
        _gatt = null;
        _gattCallback?.Dispose();
        _gattCallback = null;
        _controlChar = null;
        _isWorkoutActive = false;
        _pendingResponse = new TaskCompletionSource<byte[]?>();
        OnConnectionChanged?.Invoke(false);
        Log("Disconnected");
        return Task.CompletedTask;
    }

    private async Task<bool> AwaitConnectionAsync(int timeoutMs)
    {
        if (_connectionTcs == null) return false;

        var timeout = Task.Delay(timeoutMs);
        var done = await Task.WhenAny(_connectionTcs.Task, timeout);
        if (done == timeout) return false;

        _serviceDiscoveredTcs = new TaskCompletionSource<bool>();
        var svcTimeout = Task.Delay(8000);
        var svcDone = await Task.WhenAny(_serviceDiscoveredTcs.Task, svcTimeout);
        return svcDone != svcTimeout && _gatt?.Services?.Count > 0;
    }

    private void OnGattConnected()
    {
        Log("Connected, waiting for bonded device service changed...");
        // Xiaomi waits 1600ms for bonded devices to handle Service Changed indication
        Task.Run(async () =>
        {
            await Task.Delay(1600);
            var gatt = _gatt;
            if (gatt == null) return;

            Log("Requesting MTU...");
            gatt.RequestMtu(512);
        });
    }
}
